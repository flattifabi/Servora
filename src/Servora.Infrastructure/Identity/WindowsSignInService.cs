using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Identity;
using Servora.Application.Identity.Ldap;
using Servora.Application.Identity.Windows;
using Servora.Application.Observability;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Infrastructure.Identity;

/// <summary>Default <see cref="IWindowsSignInService"/>.</summary>
/// <param name="configuration">The directory configuration provider.</param>
/// <param name="client">The directory client.</param>
/// <param name="provisioner">Runs provisioning as a system actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
public sealed class WindowsSignInService(
    IDirectoryConfigurationProvider configuration,
    IDirectoryClient client,
    SystemScopeProvisioner provisioner,
    IAppDbContextFactory dbFactory,
    IAuditService audit,
    IClock clock) : IWindowsSignInService
{
    /// <summary>The authentication source key.</summary>
    public const string SourceKey = "windows";

    /// <inheritdoc />
    public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default) =>
        (await configuration.GetSettingsAsync(cancellationToken)).WindowsAuthEnabled;

    /// <inheritdoc />
    public async Task<SignInResult> SignInAsync(string windowsName, string? sid, CancellationToken cancellationToken = default)
    {
        var settings = await configuration.GetSettingsAsync(cancellationToken);
        var name = (windowsName ?? string.Empty).Trim();
        if (!settings.WindowsAuthEnabled)
        {
            return await FailAsync(name, SignInOutcome.InvalidCredentials, "Windows sign-in is disabled.", cancellationToken);
        }

        var login = DirectoryLoginName.Parse(name);
        if (login is null || !login.MatchesDomain(settings.NetBiosDomain))
        {
            return await FailAsync(name, SignInOutcome.InvalidCredentials, "The Windows account does not belong to the configured domain.", cancellationToken);
        }

        sid = string.IsNullOrWhiteSpace(sid) ? null : sid.Trim();
        var userId = await FindWindowsAccountAsync(sid, cancellationToken);
        var directoryDown = false;
        var directoryConsulted = false;

        if (userId is null && settings.Enabled && settings.IsConnectionConfigured)
        {
            directoryConsulted = true;
            var (resolved, down, refusal) = await ResolveFromDirectoryAsync(settings, login, sid, cancellationToken);
            if (refusal is not null)
            {
                return await FailAsync(name, SignInOutcome.InvalidCredentials, refusal, cancellationToken);
            }

            userId = resolved;
            directoryDown = down;
        }

        // Without a reachable directory, an existing directory account with the same login name is used (the domain
        // already authenticated the Windows identity). When the directory answered, its answer is authoritative.
        if (userId is null && (!directoryConsulted || directoryDown))
        {
            userId = await FindDirectoryAccountByNameAsync(login.Account, cancellationToken);
        }

        if (userId is null && settings.WindowsAutoProvision && !settings.Enabled)
        {
            var (created, error) = await CreateWindowsAccountAsync(login, sid, settings, cancellationToken);
            if (error is not null)
            {
                return await FailAsync(name, SignInOutcome.InvalidCredentials, error, cancellationToken);
            }

            userId = created;
        }

        if (userId is not { } id)
        {
            return await FailAsync(
                name,
                directoryDown ? SignInOutcome.DirectoryUnavailable : SignInOutcome.InvalidCredentials,
                directoryDown ? "The directory is unavailable." : "No Servora account is linked to this Windows identity.",
                cancellationToken);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstAsync(u => u.Id == id, cancellationToken);
        var now = clock.UtcNow;
        if (user.Source is UserSource.Local or UserSource.Service)
        {
            return await FailAsync(name, SignInOutcome.InvalidCredentials, "Windows sign-in is not allowed for local or service accounts.", cancellationToken);
        }

        if (!user.IsActive)
        {
            return await FailAsync(name, SignInOutcome.Disabled, null, cancellationToken);
        }

        if (user.LockoutEndsAt is { } until && until > now)
        {
            return await FailAsync(name, SignInOutcome.LockedOut, null, cancellationToken);
        }

        user.FailedSignInCount = 0;
        user.LastSignInAt = now;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("auth", "login.success")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            ActorNameOverride = user.DisplayName,
            Details = new { source = SourceKey, windowsAccount = name },
        }, cancellationToken);
        return new SignInResult(SignInOutcome.Success, user, SourceKey);
    }

    private async Task<Guid?> FindWindowsAccountAsync(string? sid, CancellationToken cancellationToken)
    {
        if (sid is null)
        {
            return null;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<User>().AsNoTracking()
            .Where(u => u.Source == UserSource.Windows && u.ExternalId == sid)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<Guid?> FindDirectoryAccountByNameAsync(string account, CancellationToken cancellationToken)
    {
        var normalized = account.ToUpperInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        return await db.Set<User>().AsNoTracking()
            .Where(u => u.Source == UserSource.Directory && u.NormalizedUserName == normalized)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(Guid? UserId, bool DirectoryDown, string? Refusal)> ResolveFromDirectoryAsync(
        DirectorySettings settings,
        DirectoryLoginName login,
        string? sid,
        CancellationToken cancellationToken)
    {
        var bindPassword = await configuration.GetBindPasswordAsync(cancellationToken);
        var lookup = await client.FindUserAsync(settings, bindPassword, login, cancellationToken);
        if (lookup.Outcome is DirectoryLookupOutcome.Unavailable or DirectoryLookupOutcome.ConfigurationError)
        {
            return (null, true, null);
        }

        if (!lookup.Succeeded)
        {
            return (null, false, null);
        }

        var entry = lookup.Entry!;
        if (sid is not null && entry.Sid is not null && !string.Equals(sid, entry.Sid, StringComparison.OrdinalIgnoreCase))
        {
            return (null, false, "The Windows SID does not match the directory account.");
        }

        if (entry.IsDisabled)
        {
            return (null, false, "The directory account is disabled.");
        }

        var mappings = await configuration.GetActiveMappingsAsync(cancellationToken);
        var allowCreate = settings.JitProvisioning && settings.WindowsAutoProvision;
        var result = await provisioner.ProvisionAsync(entry, settings, mappings, allowCreate, "Windows sign-in", cancellationToken);
        return result.Succeeded ? (result.User!.Id, false, null) : (null, false, result.Skipped ? null : result.Error);
    }

    private async Task<(Guid? UserId, string? Error)> CreateWindowsAccountAsync(DirectoryLoginName login, string? sid, DirectorySettings settings, CancellationToken cancellationToken)
    {
        if (sid is null)
        {
            return (null, "Automatic provisioning requires the Windows SID, which the server did not provide.");
        }

        var userName = login.Account.Length > 100 ? login.Account[..100] : login.Account;
        var normalized = userName.ToUpperInvariant();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<User>().AnyAsync(u => u.NormalizedUserName == normalized, cancellationToken))
        {
            return (null, "Another account already uses this login name.");
        }

        var user = new User
        {
            UserName = userName,
            NormalizedUserName = normalized,
            DisplayName = userName,
            Source = UserSource.Windows,
            ExternalId = sid,
        };
        if (!string.IsNullOrWhiteSpace(settings.DefaultRoleName))
        {
            var roleName = settings.DefaultRoleName.Trim();
            var roleId = await db.Set<Role>().AsNoTracking().Where(r => r.Name == roleName).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
            if (roleId is { } id)
            {
                user.Roles.Add(new UserRole { UserId = user.Id, RoleId = id });
            }
        }

        db.Set<User>().Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "user.provisioned")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Message = "User provisioned on first Windows sign-in.",
            Details = new { windowsAccount = login.Domain is null ? login.Account : $"{login.Domain}\\{login.Account}" },
        }, cancellationToken);
        return (user.Id, null);
    }

    private async Task<SignInResult> FailAsync(string name, SignInOutcome outcome, string? reason, CancellationToken cancellationToken)
    {
        await audit.WriteAsync(new AuditEntry("auth", "login.failed", outcome == SignInOutcome.LockedOut ? AuditOutcome.Denied : AuditOutcome.Failure)
        {
            ActorNameOverride = name.Length > 256 ? name[..256] : name,
            Message = reason is null ? $"{outcome} (Windows sign-in)" : $"{outcome} (Windows sign-in): {reason}",
        }, cancellationToken);
        return new SignInResult(outcome, null, SourceKey);
    }
}
