using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Domain.Observability;
using Group = Servora.Domain.Identity.Group;

namespace Servora.Application.Identity.Admin;

/// <summary>Default <see cref="IRoleAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="permissions">The permission service (cache invalidation).</param>
/// <param name="audit">The audit service.</param>
public sealed partial class RoleAdminService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IPermissionService permissions,
    IAuditService audit) : IRoleAdminService
{
    private const int MaxRoles = 500;
    private const int MaxHoldersShown = 200;
    private const int MaxPermissionsPerRole = 1000;

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RoleListItem>>> ListAsync(string? search = null, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<IReadOnlyList<RoleListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var roles = db.Set<Role>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            roles = roles.Where(r => r.Name.ToLower().Contains(term) || r.Label.ToLower().Contains(term) || (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        var rows = await roles
            .OrderByDescending(r => r.IsSystem).ThenBy(r => r.Label)
            .Take(MaxRoles)
            .Select(r => new
            {
                r.Id, r.Name, r.Label, r.Description, r.IsSystem, PermissionCount = r.Permissions.Count,
                HasWildcard = r.Permissions.Any(p => p.Permission == Permissions.All), r.UpdatedAt,
            })
            .ToListAsync(cancellationToken);
        var ids = rows.Select(r => r.Id).ToList();
        var userCounts = await db.Set<UserRole>().AsNoTracking().Where(r => ids.Contains(r.RoleId)).GroupBy(r => r.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var groupCounts = await db.Set<GroupRole>().AsNoTracking().Where(r => ids.Contains(r.RoleId)).GroupBy(r => r.RoleId).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        IReadOnlyList<RoleListItem> items = rows.Select(r => new RoleListItem(
            r.Id, r.Name, r.Label, r.Description, r.IsSystem, r.PermissionCount, r.HasWildcard,
            userCounts.GetValueOrDefault(r.Id), groupCounts.GetValueOrDefault(r.Id), r.UpdatedAt)).ToList();
        return Result<IReadOnlyList<RoleListItem>>.Success(items);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<RoleOption>>> GetOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<IReadOnlyList<RoleOption>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        IReadOnlyList<RoleOption> options = await db.Set<Role>().AsNoTracking()
            .OrderBy(r => r.Label)
            .Take(MaxRoles)
            .Select(r => new RoleOption(r.Id, r.Name, r.Label, r.IsSystem, r.Permissions.Count))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<RoleOption>>.Success(options);
    }

    /// <inheritdoc />
    public async Task<Result<RoleDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<RoleDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var role = await db.Set<Role>().AsNoTracking().Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return Result<RoleDetail>.NotFound();
        }

        var holders = db.Set<UserRole>().AsNoTracking().Where(r => r.RoleId == id);
        var userCount = await holders.CountAsync(cancellationToken);
        var userIds = await holders.Select(r => new { r.UserId, r.IsFromDirectory }).Take(MaxHoldersShown).ToListAsync(cancellationToken);
        var ids = userIds.Select(u => u.UserId).ToList();
        var users = await db.Set<User>().AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken);
        var groups = await db.Set<GroupRole>().AsNoTracking().Where(r => r.RoleId == id)
            .Join(db.Set<Group>().AsNoTracking(), r => r.GroupId, g => g.Id, (r, g) => new RoleGroupItem(g.Id, g.Name, g.Members.Count, g.IsActive))
            .Take(MaxRoles)
            .ToListAsync(cancellationToken);
        var mappings = await db.Set<DirectoryGroupMapping>().AsNoTracking().Where(m => m.RoleId == id).Select(m => m.DirectoryGroup).Take(50).ToListAsync(cancellationToken);

        return Result<RoleDetail>.Success(new RoleDetail
        {
            Id = role.Id,
            Name = role.Name,
            Label = role.Label,
            Description = role.Description,
            IsSystem = role.IsSystem,
            Permissions = role.Permissions.Select(p => p.Permission).OrderBy(p => p, StringComparer.Ordinal).ToList(),
            Users = userIds.Where(u => users.ContainsKey(u.UserId))
                .Select(u => new RoleHolderItem(u.UserId, users[u.UserId].DisplayName, users[u.UserId].UserName, users[u.UserId].IsActive, u.IsFromDirectory))
                .OrderBy(u => u.DisplayName).ToList(),
            UserCount = userCount,
            Groups = groups.OrderBy(g => g.Name).ToList(),
            DirectoryMappings = mappings,
            CreatedAt = role.CreatedAt,
            UpdatedAt = role.UpdatedAt,
            UpdatedBy = role.UpdatedBy,
            Version = role.Version,
            IsAdministratorRole = role.Name == BootstrapAccount.AdminRoleName,
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(RoleEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageRoles, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var name = model.Name?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!RoleNameRegex().IsMatch(name))
        {
            return Result<Guid>.Invalid("The technical name must be 2–100 lower case letters, digits or underscores and start with a letter.");
        }

        if (ValidateCommon(model, out var requested) is { } error)
        {
            return Result<Guid>.Invalid(error);
        }

        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, requested, cancellationToken);
        if (missing.Count > 0)
        {
            await AuditDeniedAsync(null, name, missing, cancellationToken);
            return Result<Guid>.Forbidden(PrivilegeEscalationGuard.Describe(missing));
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Set<Role>().AnyAsync(r => r.Name == name, cancellationToken))
        {
            return Result<Guid>.Conflict("A role with this technical name already exists.");
        }

        var role = new Role { Name = name, Label = model.Label.Trim(), Description = Clean(model.Description) };
        foreach (var permission in requested)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
        }

        db.Set<Role>().Add(role);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "role.created")
        {
            TargetType = "sys_role",
            TargetId = role.Id.ToString(),
            TargetName = role.Name,
            Details = new { permissions = requested },
        }, cancellationToken);
        return Result<Guid>.Success(role.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, RoleEditModel model, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageRoles, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (ValidateCommon(model, out var requested) is { } error)
        {
            return Result.Invalid(error);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var role = await db.Set<Role>().Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return Result.NotFound();
        }

        if (role.Version != version)
        {
            return Result.Conflict("The role was changed by someone else. Reload the page and apply your changes again.");
        }

        // An empty name keeps the current technical name.
        var name = string.IsNullOrWhiteSpace(model.Name) ? role.Name : model.Name.Trim().ToLowerInvariant();
        if (role.IsSystem && name != role.Name)
        {
            return Result.Invalid("The technical name of a system role cannot be changed.");
        }

        if (!role.IsSystem && name != role.Name)
        {
            if (!RoleNameRegex().IsMatch(name))
            {
                return Result.Invalid("The technical name must be 2–100 lower case letters, digits or underscores and start with a letter.");
            }

            if (await db.Set<Role>().AnyAsync(r => r.Id != id && r.Name == name, cancellationToken))
            {
                return Result.Conflict("A role with this technical name already exists.");
            }
        }

        var current = role.Permissions.Select(p => p.Permission).ToHashSet(StringComparer.Ordinal);
        if (role.Name == BootstrapAccount.AdminRoleName && !requested.Contains(Permissions.All))
        {
            return Result.Invalid("The administrator role must keep the wildcard permission (*).");
        }

        var added = requested.Where(p => !current.Contains(p)).ToList();
        var removed = current.Where(p => !requested.Contains(p)).ToList();
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, added, cancellationToken);
        if (missing.Count > 0)
        {
            await AuditDeniedAsync(role.Id, role.Name, missing, cancellationToken);
            return Result.Forbidden(PrivilegeEscalationGuard.Describe(missing));
        }

        if (removed.Contains(Permissions.All))
        {
            var holderIds = await db.Set<UserRole>().AsNoTracking().Where(r => r.RoleId == id).Select(r => r.UserId).ToListAsync(cancellationToken);
            var admins = await SuperAdminGuard.GetActiveSuperAdminIdsAsync(db, cancellationToken);
            if (admins.Count > 0 && admins.All(holderIds.Contains))
            {
                return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
            }
        }

        if (!role.IsSystem)
        {
            role.Name = name;
        }

        role.Label = model.Label.Trim();
        role.Description = Clean(model.Description);
        foreach (var permission in role.Permissions.Where(p => removed.Contains(p.Permission)).ToList())
        {
            role.Permissions.Remove(permission);
            db.Set<RolePermission>().Remove(permission);
        }

        foreach (var permission in added)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
        }

        // Make sure the version moves even when only permissions changed.
        role.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The role was changed by someone else. Reload the page and apply your changes again.");
        }

        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "role.updated")
        {
            TargetType = "sys_role",
            TargetId = role.Id.ToString(),
            TargetName = role.Name,
            Details = new { added, removed, label = role.Label },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageRoles, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var role = await db.Set<Role>().Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return Result.NotFound();
        }

        if (role.IsSystem)
        {
            return Result.Invalid("System roles cannot be deleted.");
        }

        // Deleting a role revokes it from users that may hold more permissions than the actor: same guard as editing them.
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, role.Permissions.Select(p => p.Permission), cancellationToken);
        if (missing.Count > 0)
        {
            await AuditDeniedAsync(role.Id, role.Name, missing, cancellationToken);
            return Result.Forbidden("You can only delete roles whose permissions you hold yourself.");
        }

        if (role.Permissions.Any(p => p.Permission == Permissions.All))
        {
            var holderIds = await db.Set<UserRole>().AsNoTracking().Where(r => r.RoleId == id).Select(r => r.UserId).ToListAsync(cancellationToken);
            var admins = await SuperAdminGuard.GetActiveSuperAdminIdsAsync(db, cancellationToken);
            if (admins.Count > 0 && admins.All(holderIds.Contains))
            {
                return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
            }
        }

        foreach (var mapping in await db.Set<DirectoryGroupMapping>().Where(m => m.RoleId == id).ToListAsync(cancellationToken))
        {
            mapping.RoleId = null;
            if (mapping.GroupId is null)
            {
                db.Set<DirectoryGroupMapping>().Remove(mapping);
            }
        }

        db.Set<Role>().Remove(role);
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate();
        await audit.WriteAsync(new AuditEntry("identity", "role.deleted")
        {
            TargetType = "sys_role",
            TargetId = role.Id.ToString(),
            TargetName = role.Name,
        }, cancellationToken);
        return Result.Success();
    }

    private static string? ValidateCommon(RoleEditModel model, out List<string> permissionKeys)
    {
        permissionKeys = (model.Permissions ?? []).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var label = model.Label?.Trim() ?? string.Empty;
        if (label.Length is < 2 or > 200)
        {
            return "The label must be between 2 and 200 characters.";
        }

        if (model.Description is { Length: > 2000 })
        {
            return "The description must not exceed 2000 characters.";
        }

        if (permissionKeys.Count > MaxPermissionsPerRole)
        {
            return $"A role can hold at most {MaxPermissionsPerRole} permissions.";
        }

        var unknown = permissionKeys.Where(p => p.Length > 200 || !PermissionCatalog.IsKnown(p)).Take(5).ToList();
        return unknown.Count > 0 ? $"Unknown permission key(s): {string.Join(", ", unknown)}." : null;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Task AuditDeniedAsync(Guid? roleId, string roleName, IReadOnlyList<string> missing, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry("identity", "role.grant_denied", AuditOutcome.Denied)
        {
            TargetType = "sys_role",
            TargetId = roleId?.ToString(),
            TargetName = roleName,
            Message = "Privilege escalation prevented.",
            Details = new { missing },
        }, cancellationToken);

    [GeneratedRegex("^[a-z][a-z0-9_]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex RoleNameRegex();
}
