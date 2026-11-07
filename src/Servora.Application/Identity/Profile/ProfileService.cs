using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Observability;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Application.Identity.Profile;

/// <summary>Default <see cref="IProfileService"/>.</summary>
/// <param name="currentUser">The current actor.</param>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="permissions">The permission service.</param>
/// <param name="audit">The audit service.</param>
/// <param name="sessionInvalidators">Host session invalidators (optional).</param>
public sealed class ProfileService(
    ICurrentUser currentUser,
    IAppDbContextFactory dbFactory,
    IPermissionService permissions,
    IAuditService audit,
    IEnumerable<IUserSessionInvalidator> sessionInvalidators) : IProfileService
{
    private static readonly string[] s_themes = ["dark", "light", "system"];

    /// <inheritdoc />
    public async Task<Result<MyProfile>> GetAsync(CancellationToken cancellationToken = default)
    {
        if (OwnUserId() is not { } userId)
        {
            return Result<MyProfile>.Forbidden("Only signed-in users have a profile.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result<MyProfile>.NotFound();
        }

        var managerName = user.ManagerId is { } managerId
            ? await db.Set<User>().AsNoTracking().Where(u => u.Id == managerId).Select(u => u.DisplayName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var direct = await db.Set<UserRole>().AsNoTracking().Where(r => r.UserId == userId)
            .Select(r => new { r.Role!.Label, r.Role.Description, r.IsFromDirectory })
            .ToListAsync(cancellationToken);
        var groups = await db.Set<GroupMember>().AsNoTracking().Where(m => m.UserId == userId)
            .Select(m => new { m.GroupId, m.Group!.Name, m.Group.Type, m.Group.Description, m.Group.IsActive, m.IsFromDirectory })
            .ToListAsync(cancellationToken);
        var activeGroupIds = groups.Where(g => g.IsActive).Select(g => g.GroupId).ToList();
        var viaGroups = await db.Set<GroupRole>().AsNoTracking().Where(r => activeGroupIds.Contains(r.GroupId))
            .Select(r => new { r.GroupId, r.Role!.Label, r.Role.Description })
            .ToListAsync(cancellationToken);
        var groupNames = groups.ToDictionary(g => g.GroupId, g => g.Name);
        var effective = await permissions.GetUserPermissionsAsync(userId, cancellationToken);

        var roles = direct.Select(r => new MyRole(r.Label, r.Description, r.IsFromDirectory ? "Directory" : "Direct"))
            .Concat(viaGroups.Select(r => new MyRole(r.Label, r.Description, groupNames.GetValueOrDefault(r.GroupId) ?? "Group")))
            .OrderBy(r => r.Label)
            .ToList();

        return Result<MyProfile>.Success(new MyProfile
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
            ManagerName = managerName,
            TimeZone = user.TimeZone,
            Theme = user.Theme,
            Source = user.Source,
            LastSignInAt = user.LastSignInAt,
            CreatedAt = user.CreatedAt,
            Roles = roles,
            Groups = groups.Where(g => g.IsActive).Select(g => new MyGroup(g.Name, g.Type, g.Description, g.IsFromDirectory)).OrderBy(g => g.Name).ToList(),
            PermissionCount = effective.Count,
            IsAdministrator = effective.Contains(Permissions.All),
        });
    }

    /// <inheritdoc />
    public async Task<Result> UpdateAsync(MyProfileUpdate update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (OwnUserId() is not { } userId)
        {
            return Result.Forbidden("Only signed-in users have a profile.");
        }

        if (update.Phone is { Length: > 64 })
        {
            return Result.Invalid("The phone number must not exceed 64 characters.");
        }

        if (!string.IsNullOrWhiteSpace(update.TimeZone) && (update.TimeZone.Length > 64 || !TimeZoneInfo.TryFindSystemTimeZoneById(update.TimeZone, out _)))
        {
            return Result.Invalid("The selected time zone is not known on this server.");
        }

        if (!string.IsNullOrWhiteSpace(update.Theme) && !s_themes.Contains(update.Theme))
        {
            return Result.Invalid("Select a valid theme.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        if (user.Source != UserSource.Directory)
        {
            var first = Clean(update.FirstName);
            var last = Clean(update.LastName);
            var display = Clean(update.DisplayName) ?? string.Join(' ', new[] { first, last }.Where(s => s is not null));
            if (display.Length is 0 or > 256 || first is { Length: > 128 } || last is { Length: > 128 })
            {
                return Result.Invalid("Enter a display name (max. 256 characters); first and last name may have up to 128 characters.");
            }

            user.FirstName = first;
            user.LastName = last;
            user.DisplayName = display;
        }

        user.Phone = Clean(update.Phone);
        user.TimeZone = Clean(update.TimeZone);
        user.Theme = Clean(update.Theme);
        await db.SaveChangesAsync(cancellationToken);
        await audit.WriteAsync(new AuditEntry("identity", "profile.updated")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
        }, cancellationToken);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> SignOutEverywhereAsync(CancellationToken cancellationToken = default)
    {
        if (OwnUserId() is not { } userId)
        {
            return Result.Forbidden("Only signed-in users have sessions.");
        }

        await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
        var user = await db.Set<User>().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null)
        {
            return Result.NotFound();
        }

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(cancellationToken);
        permissions.Invalidate(userId);
        foreach (var invalidator in sessionInvalidators)
        {
            invalidator.Invalidate(userId);
        }

        await audit.WriteAsync(new AuditEntry("auth", "sessions.revoked")
        {
            TargetType = "sys_user",
            TargetId = user.Id.ToString(),
            TargetName = user.UserName,
            Message = "The user signed out of all sessions.",
        }, cancellationToken);
        return Result.Success();
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private Guid? OwnUserId() => currentUser.ActorType == ActorType.User ? currentUser.UserId : null;
}
