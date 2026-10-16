using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using TaskManager.Api.Entities;

namespace TaskManager.Api.Auth;

/// <summary>
/// <see cref="UserManager{TUser}"/> whose lockout logic reads time from the injected <see cref="TimeProvider"/>
/// instead of <c>DateTimeOffset.UtcNow</c>, so lockout expiry can be tested with FakeTimeProvider (SPEC 5.6, AC-AUTH-6).
/// The logic is otherwise the same as the base class.
/// </summary>
public sealed class AppUserManager : UserManager<User>
{
    private readonly TimeProvider _timeProvider;

    public AppUserManager(
        IUserStore<User> store,
        IOptions<IdentityOptions> optionsAccessor,
        IPasswordHasher<User> passwordHasher,
        IEnumerable<IUserValidator<User>> userValidators,
        IEnumerable<IPasswordValidator<User>> passwordValidators,
        ILookupNormalizer keyNormalizer,
        IdentityErrorDescriber errors,
        IServiceProvider services,
        ILogger<UserManager<User>> logger,
        TimeProvider timeProvider)
        : base(store, optionsAccessor, passwordHasher, userValidators, passwordValidators, keyNormalizer, errors, services, logger)
    {
        _timeProvider = timeProvider;
    }

    private IUserLockoutStore<User> LockoutStore =>
        Store as IUserLockoutStore<User> ?? throw new NotSupportedException("The user store does not support lockout.");

    public override async Task<bool> IsLockedOutAsync(User user)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        if (!await LockoutStore.GetLockoutEnabledAsync(user, CancellationToken))
        {
            return false;
        }

        var lockoutEnd = await LockoutStore.GetLockoutEndDateAsync(user, CancellationToken);
        return lockoutEnd >= _timeProvider.GetUtcNow();
    }

    public override async Task<IdentityResult> AccessFailedAsync(User user)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(user);

        var count = await LockoutStore.IncrementAccessFailedCountAsync(user, CancellationToken);
        if (count < Options.Lockout.MaxFailedAccessAttempts)
        {
            return await UpdateUserAsync(user);
        }

        Logger.LogInformation("User is locked out after {Count} failed attempts.", count);
        await LockoutStore.SetLockoutEndDateAsync(
            user,
            _timeProvider.GetUtcNow().Add(Options.Lockout.DefaultLockoutTimeSpan),
            CancellationToken);
        await LockoutStore.ResetAccessFailedCountAsync(user, CancellationToken);
        return await UpdateUserAsync(user);
    }
}
