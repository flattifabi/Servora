using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Seeds the built-in roles and the bootstrap administrator (<c>admin</c> / <c>admin</c>). The administrator must change
/// the password at first sign-in (see ADR-006). Idempotent: the admin account is only created when no user named
/// <c>admin</c> exists.
/// </summary>
/// <remarks>
/// Default permissions are granted <b>once per role and permission</b>: the granted defaults are recorded in the system
/// setting <c>identity.role_defaults.&lt;role&gt;</c>. A default that is introduced by a newer version is therefore added
/// to existing installations at the next start, while a default that an administrator removed later stays removed
/// (the <c>admin</c> role always keeps <c>*</c>). Installations without the marker (older versions re-added every missing
/// default at each start) get all missing defaults once and are then tracked.
/// </remarks>
/// <param name="hasher">The password hasher.</param>
/// <param name="logger">The logger.</param>
public sealed class IdentitySeeder(IPasswordHasherService hasher, ILogger<IdentitySeeder> logger) : IDataSeeder
{
    /// <summary>Login name of the bootstrap administrator.</summary>
    public const string AdminUserName = "admin";

    /// <summary>Prefix of the system settings that record the default permissions already granted to a built-in role.</summary>
    public const string AppliedDefaultsKeyPrefix = "identity.role_defaults.";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var roles = await db.Set<Role>().Include(r => r.Permissions).ToListAsync(cancellationToken);
        var markers = await db.Set<SystemSetting>().Where(s => s.Key.StartsWith(AppliedDefaultsKeyPrefix)).Take(500).ToListAsync(cancellationToken);
        foreach (var definition in BuiltInRoles.All)
        {
            var role = roles.FirstOrDefault(r => r.Name == definition.Name);
            if (role is null)
            {
                role = new Role { Name = definition.Name, Label = definition.Label, Description = definition.Description, IsSystem = true };
                db.Set<Role>().Add(role);
                roles.Add(role);
            }

            var markerKey = AppliedDefaultsKeyPrefix + definition.Name;
            var marker = markers.FirstOrDefault(m => m.Key == markerKey);
            var applied = ParseApplied(marker?.Value);
            foreach (var permission in definition.Permissions)
            {
                var alwaysEnforced = definition.Name == BuiltInRoles.Admin && permission == Permissions.All;
                if ((alwaysEnforced || !applied.Contains(permission)) && role.Permissions.All(rp => rp.Permission != permission))
                {
                    // Added explicitly: entities with a pre-set key that are only attached through the navigation of a
                    // tracked role would be treated as existing rows (UPDATE) by EF Core.
                    var grant = new RolePermission { RoleId = role.Id, Permission = permission };
                    db.Set<RolePermission>().Add(grant);
                    role.Permissions.Add(grant);
                }
            }

            applied.UnionWith(definition.Permissions);
            var value = string.Join(' ', applied.Order(StringComparer.Ordinal));
            if (marker is null)
            {
                db.Set<SystemSetting>().Add(new SystemSetting
                {
                    Key = markerKey,
                    Value = value,
                    Category = "Identity",
                    ValueType = "string",
                    Description = "Default permissions already granted to this built-in role (removed defaults are not granted again).",
                });
            }
            else if (marker.Value != value)
            {
                marker.Value = value;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        var normalized = AdminUserName.ToUpperInvariant();
        if (!await db.Set<User>().AnyAsync(u => u.NormalizedUserName == normalized, cancellationToken))
        {
            var adminRole = roles.First(r => r.Name == BuiltInRoles.Admin);
            var admin = new User
            {
                UserName = AdminUserName,
                NormalizedUserName = normalized,
                DisplayName = "System Administrator",
                FirstName = "System",
                LastName = "Administrator",
                Email = "admin@localhost",
                Source = UserSource.Local,
                PasswordHash = hasher.Hash("admin"),
                MustChangePassword = true,
                Title = "Administrator",
            };
            admin.Roles.Add(new UserRole { UserId = admin.Id, RoleId = adminRole.Id });
            db.Set<User>().Add(admin);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Bootstrap administrator 'admin' created with the default password. The password must be changed at first sign-in.");
        }
    }

    private static HashSet<string> ParseApplied(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? new HashSet<string>(StringComparer.Ordinal)
            : value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet(StringComparer.Ordinal);
}
