using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Servora.Application.Common;
using Servora.Application.Identity;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Identity;

namespace Servora.Infrastructure.Identity;

/// <summary>
/// Seeds demo groups, users and memberships once per installation (guarded by the system property
/// <see cref="MarkerKey"/>, so deleted demo records are not recreated). Demo users get a random password that is
/// hashed immediately and never shown or logged, and must change it at first sign-in – an administrator has to reset
/// the password before a demo user can be used.
/// </summary>
/// <param name="hasher">The password hasher.</param>
/// <param name="logger">The logger.</param>
public sealed class IdentityDemoSeeder(IPasswordHasherService hasher, ILogger<IdentityDemoSeeder> logger) : IDataSeeder
{
    /// <summary>The system property recording that demo data was seeded.</summary>
    public const string MarkerKey = "identity.demo_data.seeded";

    private static readonly (string Name, GroupType Type, string Description, string Email, string[] Roles)[] s_groups =
    [
        ("Service Desk", GroupType.Assignment, "First line support: receives, triages and resolves incidents and requests.", "servicedesk@example.com", [BuiltInRoles.Itil]),
        ("Network Operations", GroupType.Assignment, "Operates WAN, LAN, firewalls, VPN and wireless infrastructure.", "netops@example.com", [BuiltInRoles.Itil]),
        ("Database Administration", GroupType.Assignment, "Runs and tunes SQL Server and PostgreSQL platforms, backups and restores.", "dba@example.com", [BuiltInRoles.Itil]),
        ("Change Advisory Board", GroupType.Approval, "Reviews and authorises normal and emergency changes.", "cab@example.com", ["change_manager"]),
    ];

    private static readonly (string UserName, string First, string Last, string Title, string Department, string Location, string? Group, string Role)[] s_users =
    [
        ("olivia.hart", "Olivia", "Hart", "Service Desk Lead", "IT Operations", "Berlin", "Service Desk", BuiltInRoles.Itil),
        ("emma.clarke", "Emma", "Clarke", "Service Desk Analyst", "IT Operations", "Berlin", "Service Desk", BuiltInRoles.Itil),
        ("jonas.weber", "Jonas", "Weber", "Service Desk Analyst", "IT Operations", "Hamburg", "Service Desk", BuiltInRoles.Itil),
        ("liam.becker", "Liam", "Becker", "Network Engineer", "IT Infrastructure", "Munich", "Network Operations", BuiltInRoles.Itil),
        ("sofia.marino", "Sofia", "Marino", "Database Administrator", "IT Infrastructure", "Munich", "Database Administration", BuiltInRoles.Itil),
        ("noah.fischer", "Noah", "Fischer", "Change Manager", "IT Governance", "Berlin", "Change Advisory Board", "change_manager"),
        ("james.wilson", "James", "Wilson", "Financial Controller", "Finance", "London", null, BuiltInRoles.User),
        ("mia.schneider", "Mia", "Schneider", "HR Business Partner", "Human Resources", "Berlin", null, BuiltInRoles.User),
        ("lucas.meyer", "Lucas", "Meyer", "Account Executive", "Sales", "Vienna", null, BuiltInRoles.User),
    ];

    /// <inheritdoc />
    public int Order => 100;

    /// <inheritdoc />
    public async Task SeedAsync(IAppDbContext db, CancellationToken cancellationToken)
    {
        if (await db.Set<SystemSetting>().AnyAsync(s => s.Key == MarkerKey, cancellationToken))
        {
            return;
        }

        var roles = await db.Set<Role>().AsNoTracking().ToDictionaryAsync(r => r.Name, r => r.Id, cancellationToken);
        var groups = new Dictionary<string, Group>(StringComparer.Ordinal);
        foreach (var definition in s_groups)
        {
            var group = await db.Set<Group>().Include(g => g.Roles).FirstOrDefaultAsync(g => g.Name == definition.Name, cancellationToken);
            if (group is null)
            {
                group = new Group { Name = definition.Name, Type = definition.Type, Description = definition.Description, Email = definition.Email };
                db.Set<Group>().Add(group);
            }

            foreach (var roleName in definition.Roles)
            {
                if (roles.TryGetValue(roleName, out var roleId) && group.Roles.All(r => r.RoleId != roleId))
                {
                    group.Roles.Add(new GroupRole { GroupId = group.Id, RoleId = roleId });
                }
            }

            groups[definition.Name] = group;
        }

        await db.SaveChangesAsync(cancellationToken);

        var created = 0;
        var users = new Dictionary<string, User>(StringComparer.Ordinal);
        foreach (var definition in s_users)
        {
            var normalized = definition.UserName.ToUpperInvariant();
            var user = await db.Set<User>().Include(u => u.Roles).Include(u => u.Groups).FirstOrDefaultAsync(u => u.NormalizedUserName == normalized, cancellationToken);
            if (user is null)
            {
                user = new User
                {
                    UserName = definition.UserName,
                    NormalizedUserName = normalized,
                    FirstName = definition.First,
                    LastName = definition.Last,
                    DisplayName = $"{definition.First} {definition.Last}",
                    Email = $"{definition.UserName}@example.com",
                    Title = definition.Title,
                    Department = definition.Department,
                    Location = definition.Location,
                    Source = UserSource.Local,

                    // Random, never displayed: an administrator resets the password before the account is used.
                    PasswordHash = hasher.Hash(TemporaryPassword.Generate(32)),
                    MustChangePassword = true,
                };
                db.Set<User>().Add(user);
                created++;
            }

            // Fulfillers receive their roles through their group; employees get the role directly.
            if (definition.Group is null && roles.TryGetValue(definition.Role, out var roleId) && user.Roles.All(r => r.RoleId != roleId))
            {
                user.Roles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
            }

            if (definition.Group is { } groupName && groups.TryGetValue(groupName, out var group) && user.Groups.All(m => m.GroupId != group.Id))
            {
                user.Groups.Add(new GroupMember { UserId = user.Id, GroupId = group.Id });
            }

            users[definition.UserName] = user;
        }

        // Managers: the service desk lead manages the desk and its analysts; the change manager chairs the CAB.
        groups["Service Desk"].ManagerId ??= users["olivia.hart"].Id;
        groups["Change Advisory Board"].ManagerId ??= users["noah.fischer"].Id;
        users["emma.clarke"].ManagerId ??= users["olivia.hart"].Id;
        users["jonas.weber"].ManagerId ??= users["olivia.hart"].Id;

        db.Set<SystemSetting>().Add(new SystemSetting
        {
            Key = MarkerKey,
            Value = "true",
            Category = IdentitySettingsStore.Category,
            ValueType = "bool",
            Description = "Demo groups and users were seeded (they are not recreated after deletion).",
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded identity demo data: {Groups} groups, {Users} new users (passwords random, change required).", s_groups.Length, created);
    }
}
