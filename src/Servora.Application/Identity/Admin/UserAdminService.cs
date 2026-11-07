using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Identity.Policies;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Domain.Observability;

namespace Servora.Application.Identity.Admin;

/// <summary>Default <see cref="IUserAdminService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="permissions">The permission service.</param>
/// <param name="hasher">The password hasher.</param>
/// <param name="policyProvider">The authentication policy provider.</param>
/// <param name="audit">The audit service.</param>
/// <param name="clock">The clock.</param>
/// <param name="sessionInvalidators">Host session invalidators (optional).</param>
public sealed partial class UserAdminService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IPermissionService permissions,
    IPasswordHasherService hasher,
    IAuthPolicyProvider policyProvider,
    IAuditService audit,
    IClock clock,
    IEnumerable<IUserSessionInvalidator> sessionInvalidators) : IUserAdminService
{
    private const string CannotManageMessage = "You cannot manage this account because it holds permissions you do not have.";

    /// <inheritdoc />
    public async Task<Result<PagedResult<UserListItem>>> ListAsync(UserListQuery query, PageRequest page, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(page);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<PagedResult<UserListItem>>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.UtcNow;
        var users = db.Set<User>().AsNoTracking();
        users = query.Status switch
        {
            UserStatusFilter.Active => users.Where(u => u.IsActive),
            UserStatusFilter.Inactive => users.Where(u => !u.IsActive),
            UserStatusFilter.Locked => users.Where(u => u.LockoutEndsAt != null && u.LockoutEndsAt > now),
            _ => users,
        };

        if (query.Source is { } source)
        {
            users = users.Where(u => u.Source == source);
        }

        if (query.RoleId is { } roleId)
        {
            users = users.Where(u => u.Roles.Any(r => r.RoleId == roleId));
        }

        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var term = page.Search.Trim().ToLower();
            users = users.Where(u => u.DisplayName.ToLower().Contains(term)
                || u.UserName.ToLower().Contains(term)
                || (u.Email != null && u.Email.ToLower().Contains(term))
                || (u.Department != null && u.Department.ToLower().Contains(term))
                || (u.Title != null && u.Title.ToLower().Contains(term)));
        }

        users = (page.SortBy, page.SortDescending) switch
        {
            ("UserName", false) => users.OrderBy(u => u.UserName),
            ("UserName", true) => users.OrderByDescending(u => u.UserName),
            ("Department", false) => users.OrderBy(u => u.Department).ThenBy(u => u.DisplayName),
            ("Department", true) => users.OrderByDescending(u => u.Department).ThenBy(u => u.DisplayName),
            ("Source", false) => users.OrderBy(u => u.Source).ThenBy(u => u.DisplayName),
            ("Source", true) => users.OrderByDescending(u => u.Source).ThenBy(u => u.DisplayName),
            ("LastSignInAt", false) => users.OrderBy(u => u.LastSignInAt),
            ("LastSignInAt", true) => users.OrderByDescending(u => u.LastSignInAt),
            (_, true) => users.OrderByDescending(u => u.DisplayName),
            _ => users.OrderBy(u => u.DisplayName),
        };

        var total = await users.CountAsync(cancellationToken);
        var rows = await users.Skip(page.Skip).Take(page.PageSize)
            .Select(u => new
            {
                u.Id, u.UserName, u.DisplayName, u.Email, u.Title, u.Department, u.Source, u.IsActive, u.LockoutEndsAt,
                u.MustChangePassword, u.LastSignInAt, GroupCount = u.Groups.Count,
            })
            .ToListAsync(cancellationToken);

        var ids = rows.Select(r => r.Id).ToList();
        var roles = await db.Set<UserRole>().AsNoTracking()
            .Where(r => ids.Contains(r.UserId))
            .Select(r => new { r.UserId, r.Role!.Label })
            .ToListAsync(cancellationToken);
        var rolesByUser = roles.GroupBy(r => r.UserId).ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(x => x.Label).OrderBy(l => l).ToList());

        var items = rows.Select(r => new UserListItem(
            r.Id, r.UserName, r.DisplayName, r.Email, r.Title, r.Department, r.Source, r.IsActive,
            r.LockoutEndsAt is { } until && until > now, r.MustChangePassword, r.LastSignInAt,
            rolesByUser.GetValueOrDefault(r.Id) ?? [], r.GroupCount)).ToList();
        return Result<PagedResult<UserListItem>>.Success(new PagedResult<UserListItem>(items, total, page.Page, page.PageSize));
    }

    /// <inheritdoc />
    public async Task<Result<UserStats>> GetStatsAsync(CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<UserStats>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var now = clock.UtcNow;
        var since = now.AddDays(-30);
        var people = db.Set<User>().AsNoTracking().Where(u => u.Source != UserSource.Service);
        var stats = new UserStats(
            await people.CountAsync(cancellationToken),
            await people.CountAsync(u => u.IsActive, cancellationToken),
            await people.CountAsync(u => u.Source == UserSource.Directory || u.Source == UserSource.Windows, cancellationToken),
            await people.CountAsync(u => u.LockoutEndsAt != null && u.LockoutEndsAt > now, cancellationToken),
            await people.CountAsync(u => u.LastSignInAt != null && u.LastSignInAt > since, cancellationToken));
        return Result<UserStats>.Success(stats);
    }

    /// <inheritdoc />
    public async Task<Result<UserDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ReadUsers, cancellationToken))
        {
            return Result<UserDetail>.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result<UserDetail>.NotFound();
        }

        var roles = await db.Set<UserRole>().AsNoTracking().Where(r => r.UserId == id)
            .Select(r => new UserRoleItem(r.RoleId, r.Role!.Name, r.Role.Label, r.Role.IsSystem, r.IsFromDirectory))
            .ToListAsync(cancellationToken);
        var groups = await db.Set<GroupMember>().AsNoTracking().Where(m => m.UserId == id)
            .Select(m => new { m.GroupId, m.Group!.Name, m.Group.Type, m.IsFromDirectory, m.Group.IsActive })
            .ToListAsync(cancellationToken);
        var activeGroupIds = groups.Where(g => g.IsActive).Select(g => g.GroupId).ToList();
        var inherited = await db.Set<GroupRole>().AsNoTracking().Where(r => activeGroupIds.Contains(r.GroupId))
            .Select(r => new { r.RoleId, r.Role!.Label, r.GroupId })
            .ToListAsync(cancellationToken);
        var groupNames = groups.ToDictionary(g => g.GroupId, g => g.Name);
        var effective = await permissions.GetUserPermissionsAsync(id, cancellationToken);
        var canManage = await CanManageTargetAsync(id, cancellationToken);

        return Result<UserDetail>.Success(new UserDetail
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            Phone = user.Phone,
            Title = user.Title,
            Department = user.Department,
            Location = user.Location,
            ManagerId = user.ManagerId,
            TimeZone = user.TimeZone,
            Source = user.Source,
            ExternalId = user.ExternalId,
            DistinguishedName = user.DistinguishedName,
            IsActive = user.IsActive,
            MustChangePassword = user.MustChangePassword,
            LockoutEndsAt = user.LockoutEndsAt,
            LastSignInAt = user.LastSignInAt,
            LastSyncedAt = user.LastSyncedAt,
            CreatedAt = user.CreatedAt,
            CreatedBy = user.CreatedBy,
            UpdatedAt = user.UpdatedAt,
            UpdatedBy = user.UpdatedBy,
            Version = user.Version,
            Roles = roles.OrderBy(r => r.Label).ToList(),
            Groups = groups.Select(g => new UserGroupItem(g.GroupId, g.Name, g.Type, g.IsFromDirectory, g.IsActive)).OrderBy(g => g.Name).ToList(),
            InheritedRoles = inherited.Select(r => new InheritedRoleItem(r.RoleId, r.Label, r.GroupId, groupNames.GetValueOrDefault(r.GroupId) ?? string.Empty))
                .OrderBy(r => r.RoleLabel).ToList(),
            EffectivePermissions = effective.OrderBy(p => p, StringComparer.Ordinal).ToList(),
            IsBootstrapAdmin = BootstrapAccount.IsAdmin(user.UserName),
            IsSelf = currentUser.UserId == user.Id,
            CanManage = canManage,
        });
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(UserEditModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageUsers, cancellationToken))
        {
            return Result<Guid>.Forbidden();
        }

        var userName = model.UserName?.Trim() ?? string.Empty;
        if (!UserNameRegex().IsMatch(userName))
        {
            return Result<Guid>.Invalid("The user name must be 2–100 characters: letters, digits, '.', '_', '-' or '@'.");
        }

        if (model.Source is not (UserSource.Local or UserSource.Service))
        {
            return Result<Guid>.Invalid("Only local and service accounts can be created manually. Directory and Windows accounts are provisioned automatically.");
        }

        if (ValidateProfile(model) is { } profileError)
        {
            return Result<Guid>.Invalid(profileError);
        }

        string? hash = null;
        if (model.Source == UserSource.Local)
        {
            var policy = await policyProvider.GetPasswordPolicyAsync(cancellationToken);
            if (policy.Validate(model.InitialPassword, userName) is { } passwordError)
            {
                return Result<Guid>.Invalid(passwordError);
            }

            hash = hasher.Hash(model.InitialPassword!);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var normalized = userName.ToUpperInvariant();
        if (await db.Set<User>().AnyAsync(u => u.NormalizedUserName == normalized, cancellationToken))
        {
            return Result<Guid>.Conflict("A user with this user name already exists.");
        }

        if (await ValidateManagerAsync(db, model.ManagerId, null, cancellationToken) is { } managerError)
        {
            return Result<Guid>.Invalid(managerError);
        }

        var user = new User
        {
            UserName = userName,
            NormalizedUserName = normalized,
            Source = model.Source,
            PasswordHash = hash,
            MustChangePassword = model.Source == UserSource.Local && model.MustChangePassword,
        };
        ApplyProfile(user, model);
        db.Set<User>().Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "user.created")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Details = new { user.Source, user.MustChangePassword },
        }, cancellationToken);
        return Result<Guid>.Success(user.Id);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(Guid id, UserEditModel model, long version, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageUsers, cancellationToken))
        {
            return Result.Forbidden();
        }

        if (!await CanManageTargetAsync(id, cancellationToken))
        {
            return Result.Forbidden(CannotManageMessage);
        }

        if (ValidateProfile(model) is { } profileError)
        {
            return Result.Invalid(profileError);
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.Version != version)
        {
            return Result.Conflict("The user was changed by someone else. Reload the page and apply your changes again.");
        }

        if (await ValidateManagerAsync(db, model.ManagerId, id, cancellationToken) is { } managerError)
        {
            return Result.Invalid(managerError);
        }

        var before = new { user.DisplayName, user.FirstName, user.LastName, user.Email, user.Phone, user.Title, user.Department, user.Location, user.ManagerId, user.TimeZone };
        ApplyProfile(user, model);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Conflict("The user was changed by someone else. Reload the page and apply your changes again.");
        }

        await audit.WriteAsync(new AuditEntry("identity", "user.updated")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Details = new { before, after = new { user.DisplayName, user.FirstName, user.LastName, user.Email, user.Phone, user.Title, user.Department, user.Location, user.ManagerId, user.TimeZone } },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default)
    {
        var check = await CheckManageAsync(id, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        if (!active && currentUser.UserId == id)
        {
            return Result.Invalid("You cannot deactivate your own account.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.IsActive == active)
        {
            return Result.Success();
        }

        if (!active && await SuperAdminGuard.IsLastSuperAdminAsync(db, id, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        user.IsActive = active;
        if (!active)
        {
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        }
        else
        {
            user.LockoutEndsAt = null;
            user.FailedSignInCount = 0;
        }

        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(user.Id);
        await audit.WriteAsync(new AuditEntry("identity", active ? "user.activated" : "user.deactivated")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default)
    {
        var check = await CheckManageAsync(id, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.Source != UserSource.Local)
        {
            return Result.Invalid("Only local accounts have a Servora password. Directory passwords are managed in the directory.");
        }

        var policy = await policyProvider.GetPasswordPolicyAsync(cancellationToken);
        if (policy.Validate(newPassword, user.UserName) is { } error)
        {
            return Result.Invalid(error);
        }

        user.PasswordHash = hasher.Hash(newPassword);
        user.MustChangePassword = true;
        user.LockoutEndsAt = null;
        user.FailedSignInCount = 0;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(user.Id);
        await audit.WriteAsync(new AuditEntry("identity", "user.password_reset")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Message = "Password reset by an administrator; change required at next sign-in.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> UnlockAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageUsers, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        user.LockoutEndsAt = null;
        user.FailedSignInCount = 0;
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "user.unlocked")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> ForceSignOutAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageUsers, cancellationToken))
        {
            return Result.Forbidden();
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(user.Id);
        await audit.WriteAsync(new AuditEntry("identity", "user.signed_out")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Message = "All sessions were revoked by an administrator.",
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var check = await CheckManageAsync(id, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        if (currentUser.UserId == id)
        {
            return Result.Invalid("You cannot delete your own account.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (BootstrapAccount.IsAdmin(user.UserName))
        {
            return Result.Invalid("The bootstrap administrator cannot be deleted. Deactivate it instead once other administrators exist.");
        }

        if (await SuperAdminGuard.IsLastSuperAdminAsync(db, id, cancellationToken))
        {
            return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
        }

        db.Set<User>().Remove(user);
        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(user.Id);
        await audit.WriteAsync(new AuditEntry("identity", "user.deleted")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> AddRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var check = await CheckManageAsync(userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Id, u.UserName, u.Source }).FirstOrDefaultAsync(cancellationToken);
        var role = await db.Set<Role>().AsNoTracking().Where(r => r.Id == roleId).Select(r => new { r.Id, r.Name, r.Label }).FirstOrDefaultAsync(cancellationToken);
        if (user is null || role is null)
        {
            return Result.NotFound();
        }

        var requested = await IdentityGrants.GetRolePermissionsAsync(db, [roleId], cancellationToken);
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, requested, cancellationToken);
        if (missing.Count > 0)
        {
            await audit.WriteAsync(new AuditEntry("identity", "user.role_grant_denied", AuditOutcome.Denied)
            {
                TargetType = "sys_user",
                TargetId = user.Id.ToString(),
                TargetName = user.UserName,
                Message = "Privilege escalation prevented.",
                Details = new { role = role.Name, missing },
            }, cancellationToken);
            return Result.Forbidden(PrivilegeEscalationGuard.Describe(missing));
        }

        var existing = await db.Set<UserRole>().FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == roleId, cancellationToken);
        if (existing is not null)
        {
            if (!existing.IsFromDirectory)
            {
                return Result.Success();
            }

            // Convert a directory-managed assignment into a manual one so it survives directory changes.
            existing.IsFromDirectory = false;
        }
        else
        {
            db.Set<UserRole>().Add(new UserRole { UserId = userId, RoleId = roleId });
        }

        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(userId);
        await audit.WriteAsync(new AuditEntry("identity", "user.role_granted")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Details = new { role = role.Name },
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var check = await CheckManageAsync(userId, cancellationToken);
        if (!check.Succeeded)
        {
            return check;
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var assignment = await db.Set<UserRole>().Include(r => r.Role).FirstOrDefaultAsync(r => r.UserId == userId && r.RoleId == roleId, cancellationToken);
        if (assignment is null)
        {
            return Result.NotFound("The role is not assigned directly to this user.");
        }

        var grantsAll = await db.Set<RolePermission>().AnyAsync(p => p.RoleId == roleId && p.Permission == Permissions.All, cancellationToken);
        if (grantsAll && await SuperAdminGuard.IsLastSuperAdminAsync(db, userId, cancellationToken))
        {
            var remaining = await db.Set<UserRole>().AsNoTracking()
                .Where(r => r.UserId == userId && r.RoleId != roleId)
                .AnyAsync(r => db.Set<RolePermission>().Any(p => p.RoleId == r.RoleId && p.Permission == Permissions.All), cancellationToken);
            if (!remaining)
            {
                return Result.Invalid(SuperAdminGuard.LastAdministratorMessage);
            }
        }

        db.Set<UserRole>().Remove(assignment);
        await db.SaveChangesAsync(cancellationToken);
        AfterSecurityChange(userId);
        await audit.WriteAsync(new AuditEntry("identity", "user.role_revoked")
        {
            TargetType = "sys_user",
            TargetId = userId.ToString(),
            Details = new { role = assignment.Role?.Name, assignment.IsFromDirectory },
        }, cancellationToken);
        return Result.Success();
    }

    private static string? ValidateProfile(UserEditModel model)
    {
        var display = string.IsNullOrWhiteSpace(model.DisplayName)
            ? string.Join(' ', new[] { model.FirstName, model.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!.Trim()))
            : model.DisplayName.Trim();
        if (display.Length == 0)
        {
            return "Enter a display name or first and last name.";
        }

        if (display.Length > 256)
        {
            return "The display name must not exceed 256 characters.";
        }

        if (TooLong(model.FirstName, 128) || TooLong(model.LastName, 128) || TooLong(model.Title, 128) || TooLong(model.Department, 128) || TooLong(model.Location, 128))
        {
            return "Name, title, department and location must not exceed 128 characters.";
        }

        if (TooLong(model.Phone, 64))
        {
            return "The phone number must not exceed 64 characters.";
        }

        if (!string.IsNullOrWhiteSpace(model.Email) && (model.Email.Trim().Length > 320 || !EmailRegex().IsMatch(model.Email.Trim())))
        {
            return "Enter a valid e-mail address.";
        }

        if (!string.IsNullOrWhiteSpace(model.TimeZone) && (model.TimeZone.Length > 64 || !TimeZoneInfo.TryFindSystemTimeZoneById(model.TimeZone, out _)))
        {
            return "The selected time zone is not known on this server.";
        }

        return null;
    }

    private static bool TooLong(string? value, int max) => value is not null && value.Trim().Length > max;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ApplyProfile(User user, UserEditModel model)
    {
        user.FirstName = Clean(model.FirstName);
        user.LastName = Clean(model.LastName);
        user.DisplayName = string.IsNullOrWhiteSpace(model.DisplayName)
            ? string.Join(' ', new[] { user.FirstName, user.LastName }.Where(s => s is not null))
            : model.DisplayName.Trim();
        user.Email = Clean(model.Email);
        user.Phone = Clean(model.Phone);
        user.Title = Clean(model.Title);
        user.Department = Clean(model.Department);
        user.Location = Clean(model.Location);
        user.ManagerId = model.ManagerId;
        user.TimeZone = Clean(model.TimeZone);
    }

    private static async Task<string?> ValidateManagerAsync(IAppDbContext db, Guid? managerId, Guid? userId, CancellationToken cancellationToken)
    {
        if (managerId is not { } id)
        {
            return null;
        }

        if (id == userId)
        {
            return "A user cannot be their own manager.";
        }

        return await db.Set<User>().AnyAsync(u => u.Id == id, cancellationToken) ? null : "The selected manager does not exist.";
    }

    private async Task<Result> CheckManageAsync(Guid targetId, CancellationToken cancellationToken)
    {
        if (!await currentUser.HasPermissionAsync(Permissions.Identity.ManageUsers, cancellationToken))
        {
            return Result.Forbidden();
        }

        return await CanManageTargetAsync(targetId, cancellationToken) ? Result.Success() : Result.Forbidden(CannotManageMessage);
    }

    private async Task<bool> CanManageTargetAsync(Guid targetId, CancellationToken cancellationToken)
    {
        if (currentUser.UserId == targetId)
        {
            return true;
        }

        var targetPermissions = await permissions.GetUserPermissionsAsync(targetId, cancellationToken);
        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(currentUser.HasPermissionAsync, targetPermissions, cancellationToken);
        return missing.Count == 0;
    }

    private void AfterSecurityChange(Guid userId)
    {
        permissions.Invalidate(userId);
        foreach (var invalidator in sessionInvalidators)
        {
            invalidator.Invalidate(userId);
        }
    }

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9._@\-]{1,99}$", RegexOptions.CultureInvariant)]
    private static partial Regex UserNameRegex();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();
}
