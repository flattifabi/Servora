using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Seeds the built-in roles and the bootstrap administrator (<c>admin</c> / <c>admin</c>). The administrator must change
/// the password at first sign-in (see ADR-006). Idempotent: existing roles get missing default permissions added, the
/// admin account is only created when no user named <c>admin</c> exists.
/// </summary>
/// <param name="hasher">The password hasher.</param>
/// <param name="logger">The logger.</param>
public sealed class IdentitySeeder(IPasswordHasherService hasher, ILogger<IdentitySeeder> logger) : IDataSeeder
{
    /// <summary>Login name of the bootstrap administrator.</summary>
    public const string AdminUserName = "admin";

    /// <inheritdoc />
    public int Order => 0;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        var roles = await db.Set<Role>().Include(r => r.Permissions).ToListAsync(cancellationToken);
        foreach (var definition in BuiltInRoles.All)
        {
            var role = roles.FirstOrDefault(r => r.Name == definition.Name);
            if (role is null)
            {
                role = new Role { Name = definition.Name, Label = definition.Label, Description = definition.Description, IsSystem = true };
                db.Set<Role>().Add(role);
                roles.Add(role);
            }

            foreach (var permission in definition.Permissions.Where(p => role.Permissions.All(rp => rp.Permission != p)))
            {
                role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
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
}
