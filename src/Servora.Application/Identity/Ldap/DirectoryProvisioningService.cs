using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Application.Identity.Ldap;

/// <summary>
/// Default <see cref="IDirectoryProvisioningService"/>. Matching order: stable external id (objectGUID / entryUUID),
/// then login name among directory accounts. Local accounts are never linked to a directory identity automatically
/// (otherwise a directory user called <c>admin</c> could take over the local administrator). The directory is the
/// master for profile attributes and can deactivate users; reactivation is always a manual administrator decision.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="sessionInvalidators">Host session invalidators (optional).</param>
public sealed class DirectoryProvisioningService(
    IAppDbContextFactory dbFactory,
    IPermissionService permissions,
    IAuditService audit,
    IClock clock,
    IEnumerable<IUserSessionInvalidator> sessionInvalidators) : IDirectoryProvisioningService
{
    /// <inheritdoc />
    public async Task<DirectoryProvisioningResult> ProvisionAsync(
        DirectoryUserEntry entry,
        DirectorySettings settings,
        IReadOnlyList<DirectoryGroupMapping> mappings,
        bool allowCreate,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(mappings);

        var userName = entry.UserName.Trim();
        if (userName.Length is 0 or > 256)
        {
            return new DirectoryProvisioningResult(null, Error: "The directory entry has no usable login name.");
        }

        var normalized = userName.ToUpperInvariant();
        var externalId = string.IsNullOrWhiteSpace(entry.ExternalId) ? null : entry.ExternalId.Trim();
        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);

        User? user = null;
        if (externalId is not null)
        {
            user = await db.Set<User>().Include(u => u.Roles).Include(u => u.Groups)
                .FirstOrDefaultAsync(u => u.Source == UserSource.Directory && u.ExternalId == externalId, cancellationToken);
        }

        user ??= await db.Set<User>().Include(u => u.Roles).Include(u => u.Groups)
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken);

        var created = false;
        if (user is not null && user.Source != UserSource.Directory)
        {
            // A Windows account created by SSO for the same person can be upgraded to a directory account.
            var sameWindowsIdentity = user.Source == UserSource.Windows && entry.Sid is not null
                && string.Equals(user.ExternalId, entry.Sid, StringComparison.OrdinalIgnoreCase);
            if (!sameWindowsIdentity)
            {
                await audit.WriteAsync(new AuditEntry("identity", "directory.link_refused", Domain.Observability.AuditOutcome.Denied)
                {
                    TargetType = "sys_user",
                    TargetId = user.Id.ToString(),
                    TargetName = user.UserName,
                    Message = $"A {user.Source} account with the login name of directory entry '{entry.DistinguishedName}' exists and was not linked.",
                }, cancellationToken);
                return new DirectoryProvisioningResult(null, Error: "An account with the same login name already exists and is not linked to the directory.");
            }

            user.Source = UserSource.Directory;
        }

        if (user is null)
        {
            if (!allowCreate)
            {
                return new DirectoryProvisioningResult(null, Skipped: true);
            }

            if (entry.IsDisabled)
            {
                return new DirectoryProvisioningResult(null, Skipped: true, Error: "The directory account is disabled.");
            }

            user = new User
            {
                UserName = userName,
                NormalizedUserName = normalized,
                Source = UserSource.Directory,
                DisplayName = entry.EffectiveDisplayName,
            };
            db.Set<User>().Add(user);
            created = true;

            if (!string.IsNullOrWhiteSpace(settings.DefaultRoleName))
            {
                var defaultRoleName = settings.DefaultRoleName.Trim();
                var defaultRoleId = await db.Set<Role>().AsNoTracking().Where(r => r.Name == defaultRoleName).Select(r => (Guid?)r.Id).FirstOrDefaultAsync(cancellationToken);
                if (defaultRoleId is { } roleId)
                {
                    // Granted once at creation as a regular (not directory-managed) assignment.
                    user.Roles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
                }
            }
        }

        var updated = ApplyAttributes(user, entry, externalId);
        user.LastSyncedAt = clock.UtcNow;

        var deactivated = false;
        if (entry.IsDisabled && user.IsActive)
        {
            user.IsActive = false;
            user.SecurityStamp = Guid.NewGuid().ToString("N");
            deactivated = true;
        }

        var mapping = DirectoryRoleMapper.Map(entry.Groups, mappings);
        var changes = await ReconcileAsync(db, user, mapping, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);

        if (changes.Count > 0 || deactivated || created)
        {
            permissions.Invalidate(user.Id);
        }

        if (deactivated)
        {
            foreach (var invalidator in sessionInvalidators)
            {
                invalidator.Invalidate(user.Id);
            }
        }

        if (created)
        {
            await audit.WriteAsync(new AuditEntry("identity", "user.provisioned")
            {
                TargetType = "sys_user",
                TargetId = user.Id.ToString(),
                TargetName = user.UserName,
                Message = "User provisioned from the directory.",
                Details = new { entry.DistinguishedName },
            }, cancellationToken);
        }

        if (changes.Count > 0 || deactivated)
        {
            await audit.WriteAsync(new AuditEntry("identity", "user.directory_reconciled")
            {
                TargetType = "sys_user",
                TargetId = user.Id.ToString(),
                TargetName = user.UserName,
                Details = new { changes, deactivated },
            }, cancellationToken);
        }

        return new DirectoryProvisioningResult(user, created, updated && !created, deactivated, changes.Count);
    }

    private static bool ApplyAttributes(User user, DirectoryUserEntry entry, string? externalId)
    {
        var changed = false;
        changed |= Set(user.DisplayName, entry.EffectiveDisplayName, v => user.DisplayName = v!);
        changed |= Set(user.FirstName, Clip(entry.FirstName, 128), v => user.FirstName = v);
        changed |= Set(user.LastName, Clip(entry.LastName, 128), v => user.LastName = v);
        changed |= Set(user.Email, Clip(entry.Email, 320), v => user.Email = v);
        changed |= Set(user.Phone, Clip(entry.Phone, 64), v => user.Phone = v);
        changed |= Set(user.Title, Clip(entry.Title, 128), v => user.Title = v);
        changed |= Set(user.Department, Clip(entry.Department, 128), v => user.Department = v);
        changed |= Set(user.DistinguishedName, Clip(entry.DistinguishedName, 1024), v => user.DistinguishedName = v);
        if (externalId is not null)
        {
            changed |= Set(user.ExternalId, Clip(externalId, 128), v => user.ExternalId = v);
        }

        return changed;
    }

    private static bool Set(string? current, string? value, Action<string?> assign)
    {
        if (string.Equals(current, value, StringComparison.Ordinal))
        {
            return false;
        }

        assign(value);
        return true;
    }

    private static string? Clip(string? value, int max)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.Length > max ? trimmed[..max] : trimmed;
    }

    private static async Task<List<string>> ReconcileAsync(IAppDbContext db, User user, DirectoryMappingResult mapping, CancellationToken cancellationToken)
    {
        var changes = new List<string>();

        // Roles: add mapped roles that are missing, remove directory-managed roles that are no longer mapped.
        // Manually assigned roles are never touched.
        foreach (var roleId in mapping.RoleIds.Where(id => user.Roles.All(r => r.RoleId != id)))
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = roleId, IsFromDirectory = true });
            changes.Add($"role+:{roleId}");
        }

        foreach (var stale in user.Roles.Where(r => r.IsFromDirectory && !mapping.RoleIds.Contains(r.RoleId)).ToList())
        {
            user.Roles.Remove(stale);
            db.Set<UserRole>().Remove(stale);
            changes.Add($"role-:{stale.RoleId}");
        }

        if (mapping.GroupIds.Count > 0)
        {
            var existingGroups = await db.Set<Group>().AsNoTracking().Where(g => mapping.GroupIds.Contains(g.Id)).Select(g => g.Id).ToListAsync(cancellationToken);
            foreach (var groupId in existingGroups.Where(id => user.Groups.All(m => m.GroupId != id)))
            {
                user.Groups.Add(new GroupMember { UserId = user.Id, GroupId = groupId, IsFromDirectory = true });
                changes.Add($"group+:{groupId}");
            }
        }

        foreach (var stale in user.Groups.Where(m => m.IsFromDirectory && !mapping.GroupIds.Contains(m.GroupId)).ToList())
        {
            user.Groups.Remove(stale);
            db.Set<GroupMember>().Remove(stale);
            changes.Add($"group-:{stale.GroupId}");
        }

        return changes;
    }
}
