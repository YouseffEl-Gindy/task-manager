using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using TaskManager.Api.Auth;
using TaskManager.Api.Data;
using TaskManager.Api.Dtos.Account;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Entities;
using TaskManager.Api.Services;
using TaskManager.Api.UnitTests.TestSupport;

namespace TaskManager.Api.UnitTests.Services;

/// <summary>
/// AccountService with the real Identity registration (<see cref="AuthServiceExtensions.AddAppIdentity"/>) on SQLite.
/// Each call runs in its own DI scope and HttpContext, like a request.
/// </summary>
public sealed class AccountServiceTests : IDisposable
{
    private const string Password = "correcthorse1";

    private readonly SqliteTestDatabase _database = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly ServiceProvider _services;

    public AccountServiceTests()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Auth:SecurityStampValidationSeconds"] = "0" })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<TimeProvider>(_time);
        services.AddDbContext<AppDbContext>(o => o.UseSqlite(_database.Connection));
        services.AddAppIdentity(configuration);
        services.AddScoped<IAccountService, AccountService>();
        _services = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        _database.Dispose();
    }

    private async Task<T> CallAsync<T>(Func<IAccountService, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        var httpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        return await action(scope.ServiceProvider.GetRequiredService<IAccountService>());
    }

    private Task<ServiceResult<UserDto>> RegisterAsync(string email, string displayName = "Ada Lovelace", string password = Password) =>
        CallAsync(s => s.RegisterAsync(new RegisterRequest(displayName, email, password), CancellationToken.None));

    private Task<ServiceResult<UserDto>> LoginAsync(string email, string password) =>
        CallAsync(s => s.LoginAsync(new LoginRequest(email, password), CancellationToken.None));

    private static string NewEmail() => $"user{Guid.NewGuid():N}@example.com";

    [Fact]
    public async Task Register_TrimsAndLowercasesEmail_TrimsDisplayName_SetsCreatedAt()
    {
        var result = await RegisterAsync("  Ada.Lovelace@Example.COM ", " Ada Lovelace ");

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.Equal("ada.lovelace@example.com", result.Value!.Email);
        Assert.Equal("Ada Lovelace", result.Value.DisplayName);
        Assert.Equal(_time.GetUtcNow().UtcDateTime, result.Value.CreatedAt);

        await using var db = _database.CreateContext();
        var user = await db.Users.SingleAsync(u => u.Id == result.Value.Id);
        Assert.Equal("ada.lovelace@example.com", user.UserName);
        Assert.True(user.LockoutEnabled);
    }

    [Fact]
    public async Task Register_ExistingEmailInAnyCaseOrWithSpaces_IsEmailTaken()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var result = await RegisterAsync("  " + email.ToUpperInvariant() + " ");

        Assert.Equal(ServiceOutcome.EmailTaken, result.Outcome);
    }

    [Fact]
    public async Task Login_CorrectCredentials_Succeed_EmailIsNormalized()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        var result = await LoginAsync("  " + email.ToUpperInvariant(), Password);

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.Equal(email, result.Value!.Email);
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_AreBothInvalidCredentials()
    {
        var email = NewEmail();
        await RegisterAsync(email);

        Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(email, "wrongpass1")).Outcome);
        Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(NewEmail(), Password)).Outcome);
    }

    [Fact]
    public async Task Login_LockedOutAfterFiveFailures_ForFifteenMinutesOfTimeProviderTime()
    {
        var email = NewEmail();
        await RegisterAsync(email);
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(email, "wrongpass1")).Outcome);
        }

        Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(email, Password)).Outcome);

        _time.Advance(TimeSpan.FromMinutes(14));
        Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(email, Password)).Outcome);

        _time.Advance(TimeSpan.FromMinutes(1) + TimeSpan.FromSeconds(1));
        Assert.Equal(ServiceOutcome.Success, (await LoginAsync(email, Password)).Outcome);
    }

    [Fact]
    public async Task UpdateProfile_TrimsAndSavesDisplayName()
    {
        var user = (await RegisterAsync(NewEmail())).Value!;

        var result = await CallAsync(s => s.UpdateProfileAsync(user.Id, new UpdateProfileRequest("  Ada L. "), CancellationToken.None));
        var current = await CallAsync(s => s.GetCurrentAsync(user.Id, CancellationToken.None));

        Assert.Equal("Ada L.", result.Value!.DisplayName);
        Assert.Equal("Ada L.", current.Value!.DisplayName);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsValidationFailedOnCurrentPassword()
    {
        var user = (await RegisterAsync(NewEmail())).Value!;

        var result = await CallAsync(s => s.ChangePasswordAsync(user.Id, new ChangePasswordRequest("wrongpass1", "batterystaple2"), CancellationToken.None));

        Assert.Equal(ServiceOutcome.ValidationFailed, result.Outcome);
        Assert.Equal([IdentityErrorMapper.CurrentPasswordIncorrectMessage], result.Errors!.ToDictionary()["currentPassword"]);
    }

    [Fact]
    public async Task ChangePassword_Success_OldPasswordFails_NewPasswordWorks()
    {
        var email = NewEmail();
        var user = (await RegisterAsync(email)).Value!;

        var result = await CallAsync(s => s.ChangePasswordAsync(user.Id, new ChangePasswordRequest(Password, "batterystaple2"), CancellationToken.None));

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        Assert.Equal(ServiceOutcome.InvalidCredentials, (await LoginAsync(email, Password)).Outcome);
        Assert.Equal(ServiceOutcome.Success, (await LoginAsync(email, "batterystaple2")).Outcome);
    }

    [Fact]
    public async Task DeleteAccount_WrongPassword_KeepsAccountAndTasks()
    {
        var user = (await RegisterAsync(NewEmail())).Value!;
        await AddTaskAsync(user.Id);

        var result = await CallAsync(s => s.DeleteAccountAsync(user.Id, new DeleteAccountRequest("wrongpass1"), CancellationToken.None));

        Assert.Equal(ServiceOutcome.ValidationFailed, result.Outcome);
        Assert.Equal([AccountService.PasswordIncorrectMessage], result.Errors!.ToDictionary()["password"]);
        await using var db = _database.CreateContext();
        Assert.True(await db.Users.AnyAsync(u => u.Id == user.Id));
        Assert.Equal(1, await db.Tasks.CountAsync(t => t.UserId == user.Id));
    }

    [Fact]
    public async Task DeleteAccount_CorrectPassword_DeletesUserAndCascadesOnlyTheirTasks()
    {
        var user = (await RegisterAsync(NewEmail())).Value!;
        var other = (await RegisterAsync(NewEmail())).Value!;
        await AddTaskAsync(user.Id);
        await AddTaskAsync(other.Id);

        var result = await CallAsync(s => s.DeleteAccountAsync(user.Id, new DeleteAccountRequest(Password), CancellationToken.None));

        Assert.Equal(ServiceOutcome.Success, result.Outcome);
        await using var db = _database.CreateContext();
        Assert.False(await db.Users.AnyAsync(u => u.Id == user.Id));
        Assert.Equal(0, await db.Tasks.CountAsync(t => t.UserId == user.Id));
        Assert.Equal(1, await db.Tasks.CountAsync(t => t.UserId == other.Id));
        Assert.Equal(ServiceOutcome.Unauthorized, (await CallAsync(s => s.GetCurrentAsync(user.Id, CancellationToken.None))).Outcome);
    }

    private async Task AddTaskAsync(Guid userId)
    {
        await using var db = _database.CreateContext();
        var result = await new TaskService(db, _time).CreateAsync(
            userId,
            new Dtos.Tasks.CreateTaskRequest("t", null, null, null, null),
            CancellationToken.None);
        Assert.Equal(ServiceOutcome.Success, result.Outcome);
    }
}
