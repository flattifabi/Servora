using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Application.Identity;

/// <summary>
/// Default <see cref="ISignInService"/>. Local accounts are verified with the password hasher; directory accounts and
/// unknown users are delegated to registered <see cref="IExternalCredentialValidator"/>s. Lockout and auditing apply to
/// both paths. Error messages never reveal whether a user exists.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="hasher">The password hasher.</param>
/// <param name="externalValidators">External directory validators.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
public sealed class SignInService(
    IAppDbContextFactory dbFactory,
    IPasswordHasherService hasher,
    IEnumerable<IExternalCredentialValidator> externalValidators,
    IAuditService audit,
    IClock clock,
    IPermissionService permissions) : ISignInService
{
    private readonly PasswordPolicy _policy = PasswordPolicy.Default;

    /// <inheritdoc />
    public async Task<SignInResult> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        userName = (userName ?? string.Empty).Trim();
        if (userName.Length == 0 || userName.Length > 256 || string.IsNullOrEmpty(password) || password.Length > 1024)
        {
            return await FailAsync(userName, SignInOutcome.InvalidCredentials, cancellationToken);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var normalized = userName.ToUpperInvariant();
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken);
        var now = clock.UtcNow;

        if (user is not null)
        {
            if (user.LockoutEndsAt is { } until && until > now)
            {
                return await FailAsync(userName, SignInOutcome.LockedOut, cancellationToken);
            }

            if (!user.IsActive)
            {
                return await FailAsync(userName, SignInOutcome.Disabled, cancellationToken);
            }

            if (user.Source == UserSource.Service)
            {
                return await FailAsync(userName, SignInOutcome.InvalidCredentials, cancellationToken);
            }
        }

        if (user is { Source: UserSource.Local })
        {
            if (user.PasswordHash is not null && hasher.Verify(user.PasswordHash, password))
            {
                return await SucceedAsync(db, user, "local", now, cancellationToken);
            }

            await RegisterFailureAsync(db, user, now, cancellationToken);
            return await FailAsync(userName, SignInOutcome.InvalidCredentials, cancellationToken);
        }

        // Directory / Windows accounts and unknown users: try the external validators.
        var directoryDown = false;
        foreach (var validator in externalValidators)
        {
            if (!await validator.IsEnabledAsync(cancellationToken))
            {
                continue;
            }

            var (outcome, externalUser) = await validator.ValidateAsync(userName, password, cancellationToken);
            if (outcome == SignInOutcome.Success && externalUser is not null)
            {
                var tracked = await db.Set<User>().FirstAsync(u => u.Id == externalUser.Id, cancellationToken);
                if (!tracked.IsActive)
                {
                    return await FailAsync(userName, SignInOutcome.Disabled, cancellationToken);
                }

                return await SucceedAsync(db, tracked, validator.Source, now, cancellationToken);
            }

            directoryDown |= outcome == SignInOutcome.DirectoryUnavailable;
        }

        if (user is not null)
        {
            await RegisterFailureAsync(db, user, now, cancellationToken);
        }

        return await FailAsync(userName, directoryDown ? SignInOutcome.DirectoryUnavailable : SignInOutcome.InvalidCredentials, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.Source != UserSource.Local)
        {
            return Result.Invalid("Passwords of directory accounts are managed in the directory.");
        }

        if (user.PasswordHash is null || !hasher.Verify(user.PasswordHash, currentPassword ?? string.Empty))
        {
            await audit.WriteAsync(new AuditEntry("auth", "password.change", AuditOutcome.Failure) { TargetType = "sys_user", TargetId = user.Id.ToString(), TargetName = user.UserName, Message = "Current password did not match." }, cancellationToken);
            return Result.Invalid("The current password is incorrect.");
        }

        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            return Result.Invalid("The new password must be different from the current password.");
        }

        if (_policy.Validate(newPassword, user.UserName) is { } error)
        {
            return Result.Invalid(error);
        }

        user.PasswordHash = hasher.Hash(newPassword);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate(user.Id);
        await audit.WriteAsync(new AuditEntry("auth", "password.changed") { TargetType = "sys_user", TargetId = user.Id.ToString(), TargetName = user.UserName }, cancellationToken);
        return Result.Success();
    }

    private async Task<SignInResult> SucceedAsync(IAppDbContext db, User user, string source, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.FailedSignInCount = 0;
        user.LockoutEndsAt = null;
        user.LastSignInAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("auth", "login.success") { TargetType = "sys_user", TargetId = user.Id.ToString(), TargetName = user.UserName, ActorNameOverride = user.DisplayName, Details = new { source } }, cancellationToken);
        return new SignInResult(SignInOutcome.Success, user, source);
    }

    private async Task RegisterFailureAsync(IAppDbContext db, User user, DateTimeOffset now, CancellationToken cancellationToken)
    {
        user.FailedSignInCount++;
        if (user.FailedSignInCount >= _policy.MaxFailedAttempts)
        {
            user.LockoutEndsAt = now.Add(_policy.LockoutDuration);
            user.FailedSignInCount = 0;
            await audit.WriteAsync(new AuditEntry("auth", "account.locked", AuditOutcome.Denied) { TargetType = "sys_user", TargetId = user.Id.ToString(), TargetName = user.UserName }, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<SignInResult> FailAsync(string userName, SignInOutcome outcome, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry("auth", "login.failed", outcome == SignInOutcome.LockedOut ? AuditOutcome.Denied : AuditOutcome.Failure)
        {
            ActorNameOverride = userName.Length > 256 ? userName[..256] : userName,
            Message = outcome.ToString(),
        }, cancellationToken);
        return new SignInResult(outcome);
    }
}
