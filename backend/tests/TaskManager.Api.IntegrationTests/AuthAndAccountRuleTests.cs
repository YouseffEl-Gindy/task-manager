using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using TaskManager.Api.Dtos.Auth;
using TaskManager.Api.Dtos.Tasks;
using TaskManager.Api.IntegrationTests.Infrastructure;
using static TaskManager.Api.IntegrationTests.Infrastructure.ApiTestHelpers;

namespace TaskManager.Api.IntegrationTests;

/// <summary>
/// HTTP-level tests for the auth and account acceptance criteria (SPEC 9) that were only unit-tested before:
/// AC-AUTH-2, 3, 4, AC-PROFILE-2, AC-PWD-2, 3, AC-DELACCT-1 (expired cookie), AC-DELACCT-2.
/// Provider-neutral: unique users per test, ASCII-only case variants.
/// </summary>
public sealed class AuthAndAccountRuleTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public AuthAndAccountRuleTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    // ---------- AC-AUTH-2 ----------

    [Fact]
    public async Task AC_AUTH_2_RegisterExistingEmail_InAnyLetterCaseOrWithExtraSpaces_Is409EmailTaken()
    {
        using var first = _factory.CreateApiClient();
        var email = NewEmail(); // lowercase ASCII
        await RegisterAsync(first, email);

        foreach (var variant in new[] { email, email.ToUpperInvariant(), "  " + email + "  ", "\t" + MixCase(email) + " " })
        {
            using var client = _factory.CreateApiClient();
            var response = await client.PostAsJsonAsync(
                "/api/v1/auth/register",
                new { displayName = "Duplicate", email = variant, password = Password },
                ApiJson);

            var error = await AssertApiErrorAsync(response, HttpStatusCode.Conflict, "email_taken");
            Assert.Equal("An account with this email already exists.", error.GetProperty("message").GetString());
            Assert.False(response.Headers.Contains("Set-Cookie"), $"'{variant}' must not get a session");
        }

        Assert.Equal(1, await _factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == email)));
    }

    // ---------- AC-AUTH-3 ----------

    [Theory]
    [InlineData("displayName", "51 chars", null, null)]
    [InlineData("displayName", "51 chars after trim", null, null)]
    [InlineData("email", null, "no-at-sign.example.com", null)]
    [InlineData("email", null, "two@@example.com", null)]
    [InlineData("email", null, "no-dot@example", null)]
    [InlineData("email", null, "has space@example.com", null)]
    [InlineData("password", null, null, "abcdefgh")]           // no digit
    [InlineData("password", null, null, "12345678")]           // no letter
    [InlineData("password", null, null, "abc1234")]            // 7 chars
    [InlineData("password", null, null, "129 chars")]          // over 128
    public async Task AC_AUTH_3_RegisterBreakingAFieldRule_Is400_KeyedByThatField(string field, string? displayName, string? email, string? password)
    {
        using var client = _factory.CreateApiClient();
        var body = new
        {
            displayName = displayName switch
            {
                "51 chars" => new string('n', 51),
                "51 chars after trim" => "  " + new string('n', 51) + "  ",
                _ => "Valid Name",
            },
            email = email ?? NewEmail(),
            password = password == "129 chars" ? new string('a', 128) + "1" : password ?? Password,
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", body, ApiJson);

        var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal([field], ErrorKeys(error));
        Assert.False(await _factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Email == body.email)));
    }

    [Fact]
    public async Task AC_AUTH_3_RegisterAtTheLimits_Succeeds()
    {
        using var client = _factory.CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/register",
            new { displayName = "  " + new string('n', 50) + "  ", email = NewEmail(), password = new string('a', 127) + "1" },
            ApiJson);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(new string('n', 50), (await response.Content.ReadFromJsonAsync<UserDto>(ApiJson))!.DisplayName);
    }

    // ---------- AC-AUTH-4 ----------

    [Fact]
    public async Task AC_AUTH_4_LoginWithCorrectCredentials_Returns200UserDto_AndSetsTheSessionCookie()
    {
        using var registration = _factory.CreateApiClient();
        var email = NewEmail();
        var registered = await RegisterAsync(registration, email);

        using var raw = _factory.CreateApiClient(handleCookies: false);
        var response = await LoginAsync(raw, "  " + email.ToUpperInvariant() + " ");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(registered, await response.Content.ReadFromJsonAsync<UserDto>(ApiJson));
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tm_session=", StringComparison.Ordinal));
        var attributes = cookie.Split(';').Skip(1).Select(a => a.Trim().ToLowerInvariant()).ToList();
        Assert.Contains("httponly", attributes);
        Assert.Contains("samesite=lax", attributes);
        Assert.Contains("path=/", attributes);
        Assert.DoesNotContain(attributes, a => a.StartsWith("expires=") || a.StartsWith("max-age=") || a.StartsWith("domain="));

        // The cookie is a working session.
        using var session = _factory.CreateApiClient();
        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(session, email)).StatusCode);
        Assert.Equal(registered, await session.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
    }

    // ---------- AC-PROFILE-2 ----------

    [Fact]
    public async Task AC_PROFILE_2_BlankOrTooLongDisplayName_Is400KeyedDisplayName_AndNameIsUnchanged()
    {
        using var client = _factory.CreateApiClient();
        var user = await RegisterAsync(client);

        foreach (var displayName in new[] { "", "   ", new string('n', 51), "  " + new string('n', 51) + "  " })
        {
            var response = await client.PutAsJsonAsync("/api/v1/account/profile", new { displayName }, ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(["displayName"], ErrorKeys(error));
        }

        Assert.Equal(user, await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));

        // 50 chars after trimming is still valid.
        var atLimit = await client.PutAsJsonAsync("/api/v1/account/profile", new { displayName = " " + new string('m', 50) + " " }, ApiJson);
        Assert.Equal(HttpStatusCode.OK, atLimit.StatusCode);
        Assert.Equal(new string('m', 50), (await atLimit.Content.ReadFromJsonAsync<UserDto>(ApiJson))!.DisplayName);
    }

    // ---------- AC-PWD-2 / AC-PWD-3 ----------

    [Fact]
    public async Task AC_PWD_2_WrongCurrentPassword_Is400KeyedCurrentPassword_Not401_AndNothingChanges()
    {
        using var client = _factory.CreateApiClient();
        var user = await RegisterAsync(client);

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new { currentPassword = "wrongpass1", newPassword = "batterystaple2" },
            ApiJson);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["currentPassword"], ErrorKeys(error));
        Assert.Equal(["Current password is incorrect."], error.GetProperty("errors").GetProperty("currentPassword").EnumerateArray().Select(m => m.GetString()));

        _factory.Time.Advance(TimeSpan.FromSeconds(1)); // let a security-stamp re-check happen if the stamp had changed
        Assert.Equal(user, await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        await AssertLoginAsync(user.Email, Password, HttpStatusCode.OK);
        await AssertLoginAsync(user.Email, "batterystaple2", HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("abcdefgh")]      // no digit
    [InlineData("12345678")]      // no letter
    [InlineData("abc1234")]       // 7 chars
    [InlineData("129 chars")]     // over 128
    [InlineData(Password)]        // same as current
    public async Task AC_PWD_3_NewPasswordBreakingTheRulesOrEqualToCurrent_Is400KeyedNewPassword(string newPassword)
    {
        using var client = _factory.CreateApiClient();
        var user = await RegisterAsync(client);
        if (newPassword == "129 chars")
        {
            newPassword = new string('a', 128) + "1";
        }

        var response = await client.PostAsJsonAsync(
            "/api/v1/account/change-password",
            new { currentPassword = Password, newPassword },
            ApiJson);

        var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
        Assert.Equal(["newPassword"], ErrorKeys(error));
        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
        await AssertLoginAsync(user.Email, Password, HttpStatusCode.OK);
    }

    // ---------- AC-DELACCT-1 / AC-DELACCT-2 ----------

    [Fact]
    public async Task AC_DELACCT_1_DeleteAccount_Returns204_AndExpiresTheSessionCookie()
    {
        using var client = _factory.CreateApiClient();
        var user = await RegisterAsync(client);
        var task = await CreateTaskAsync(client, new { title = "goes with the account" });

        var response = await client.PostAsJsonAsync("/api/v1/account/delete", new { password = Password }, ApiJson);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("tm_session=", StringComparison.Ordinal));
        Assert.StartsWith("tm_session=;", cookie, StringComparison.Ordinal);
        Assert.Contains("expires=Thu, 01 Jan 1970", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);

        await AssertApiErrorAsync(await client.GetAsync("/api/v1/auth/me"), HttpStatusCode.Unauthorized, "unauthorized");
        await AssertLoginAsync(user.Email, Password, HttpStatusCode.Unauthorized);
        Assert.False(await _factory.WithDbAsync(db => db.Tasks.AnyAsync(t => t.Id == task.Id)));
    }

    [Fact]
    public async Task AC_DELACCT_2_WrongPassword_Is400KeyedPassword_AndAccountAndTasksRemain()
    {
        using var client = _factory.CreateApiClient();
        var user = await RegisterAsync(client);
        var tasks = new[]
        {
            await CreateTaskAsync(client, new { title = "stays 1" }),
            await CreateTaskAsync(client, new { title = "stays 2" }),
        };

        foreach (var password in new[] { "wrongpass1", Password.ToUpperInvariant(), " " + Password })
        {
            var response = await client.PostAsJsonAsync("/api/v1/account/delete", new { password }, ApiJson);
            var error = await AssertApiErrorAsync(response, HttpStatusCode.BadRequest, "validation_failed");
            Assert.Equal(["password"], ErrorKeys(error));
            Assert.Equal(["Password is incorrect."], error.GetProperty("errors").GetProperty("password").EnumerateArray().Select(m => m.GetString()));
            Assert.False(response.Headers.Contains("Set-Cookie"), "a failed delete must not touch the session cookie");
        }

        var empty = await client.PostAsJsonAsync("/api/v1/account/delete", new { password = "" }, ApiJson);
        Assert.Equal(["password"], ErrorKeys(await AssertApiErrorAsync(empty, HttpStatusCode.BadRequest, "validation_failed")));

        _factory.Time.Advance(TimeSpan.FromSeconds(1));
        Assert.Equal(user, await client.GetFromJsonAsync<UserDto>("/api/v1/auth/me", ApiJson));
        var list = (await client.GetFromJsonAsync<List<TaskDto>>("/api/v1/tasks", ApiJson))!;
        Assert.Equal(tasks.OrderBy(t => t.Id), list.OrderBy(t => t.Id));
        await AssertLoginAsync(user.Email, Password, HttpStatusCode.OK);
        Assert.True(await _factory.WithDbAsync(db => db.Users.AnyAsync(u => u.Id == user.Id)));
        Assert.Equal(2, await _factory.WithDbAsync(db => db.Tasks.CountAsync(t => t.UserId == user.Id)));
    }

    private async Task AssertLoginAsync(string email, string password, HttpStatusCode expected)
    {
        using var fresh = _factory.CreateApiClient();
        var response = await LoginAsync(fresh, email, password);
        Assert.Equal(expected, response.StatusCode);
    }

    private static string MixCase(string value) =>
        string.Concat(value.Select((c, i) => i % 2 == 0 ? char.ToUpperInvariant(c) : c));
}

/// <summary>AC-AUTH-6 in its own class: it moves the factory's FakeTimeProvider by 15 minutes.</summary>
public sealed class LockoutTests : IClassFixture<TaskManagerApiFactory>
{
    private readonly TaskManagerApiFactory _factory;

    public LockoutTests(TaskManagerApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AC_AUTH_6_FiveFailedLogins_LockTheAccountFor15Minutes_EvenForTheCorrectPassword()
    {
        using var registration = _factory.CreateApiClient();
        var email = NewEmail();
        await RegisterAsync(registration, email);
        using var client = _factory.CreateApiClient();

        for (var i = 1; i <= 5; i++)
        {
            await AssertApiErrorAsync(await LoginAsync(client, email, "wrongpass1"), HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        // 6th attempt with the CORRECT password: same response as any failed login (doesn't reveal the lockout).
        var locked = await AssertApiErrorAsync(await LoginAsync(client, email, Password), HttpStatusCode.Unauthorized, "invalid_credentials");
        Assert.Equal("Email or password is incorrect.", locked.GetProperty("message").GetString());
        Assert.False((await LoginAsync(client, email, Password)).Headers.Contains("Set-Cookie"));

        _factory.Time.Advance(TimeSpan.FromMinutes(14) + TimeSpan.FromSeconds(59));
        await AssertApiErrorAsync(await LoginAsync(client, email, Password), HttpStatusCode.Unauthorized, "invalid_credentials");

        _factory.Time.Advance(TimeSpan.FromSeconds(2)); // now 15 min 1 s after the lockout started
        var unlocked = await LoginAsync(client, email, Password);
        Assert.Equal(HttpStatusCode.OK, unlocked.StatusCode);
        Assert.Equal(email, (await unlocked.Content.ReadFromJsonAsync<UserDto>(ApiJson))!.Email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/auth/me")).StatusCode);
    }

    [Fact]
    public async Task AC_AUTH_6_FewerThanFiveFailures_DoNotLock()
    {
        using var registration = _factory.CreateApiClient();
        var email = NewEmail();
        await RegisterAsync(registration, email);
        using var client = _factory.CreateApiClient();

        for (var i = 1; i <= 4; i++)
        {
            await AssertApiErrorAsync(await LoginAsync(client, email, "wrongpass1"), HttpStatusCode.Unauthorized, "invalid_credentials");
        }

        Assert.Equal(HttpStatusCode.OK, (await LoginAsync(client, email, Password)).StatusCode);
    }
}
