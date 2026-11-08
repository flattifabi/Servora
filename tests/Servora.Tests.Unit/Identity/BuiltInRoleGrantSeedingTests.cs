using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Security;
using Servora.Domain.Configuration;
using Servora.Domain.Identity;
using Servora.Infrastructure.Identity;
using Servora.Infrastructure.Persistence;
using Servora.Infrastructure.Security;

namespace Servora.Tests.Unit.Identity;

/// <summary>
/// Default role grants (T-13): API / MCP / AI / portal defaults, granted once per role and permission so that new
/// defaults reach existing installations while defaults removed by an administrator stay removed.
/// </summary>
public sealed class BuiltInRoleGrantSeedingTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<ServoraDbContext> _options = default!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<ServoraDbContext>().UseSqlite(_connection).Options;
        await using var db = new ServoraDbContext(_options);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Fresh_installation_gets_the_documented_default_grants()
    {
        await SeedAsync();

        Assert.Contains(Permissions.Api.Access, await PermissionsOfAsync(BuiltInRoles.Itil));
        Assert.Contains(Permissions.Ai.UseAssistant, await PermissionsOfAsync(BuiltInRoles.Itil));
        Assert.DoesNotContain(Permissions.Mcp.Access, await PermissionsOfAsync(BuiltInRoles.Itil));

        var employee = await PermissionsOfAsync(BuiltInRoles.User);
        Assert.Contains(Permissions.Ai.UseAssistant, employee);
        Assert.DoesNotContain(Permissions.Api.Access, employee);
        Assert.DoesNotContain(Permissions.Mcp.Access, employee);

        Assert.Contains(Permissions.Portal.Manage, await PermissionsOfAsync(BuiltInRoles.ConfigAdmin));
        Assert.Equal([Permissions.Api.Access, Permissions.Mcp.Access], (await PermissionsOfAsync(BuiltInRoles.Integration)).Order(StringComparer.Ordinal));
        Assert.Contains(Permissions.Mcp.Access, await PermissionsOfAsync(BuiltInRoles.SecurityAdmin));

        await using var db = new ServoraDbContext(_options);
        Assert.True(await db.Set<SystemSetting>().AnyAsync(s => s.Key == IdentitySeeder.AppliedDefaultsKeyPrefix + BuiltInRoles.Itil));
    }

    [Fact]
    public async Task Existing_installation_without_marker_receives_new_defaults()
    {
        // An installation of an older version: the itil role exists without the new api.access default and no marker.
        await using (var db = new ServoraDbContext(_options))
        {
            var role = new Role { Name = BuiltInRoles.Itil, Label = "ITIL Fulfiller", IsSystem = true };
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = Permissions.Incidents.Read });
            db.Set<Role>().Add(role);
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        var permissions = await PermissionsOfAsync(BuiltInRoles.Itil);
        Assert.Contains(Permissions.Api.Access, permissions);
        Assert.Contains(Permissions.Incidents.Write, permissions);
    }

    [Fact]
    public async Task Default_removed_by_an_administrator_stays_removed()
    {
        await SeedAsync();
        await using (var db = new ServoraDbContext(_options))
        {
            var removed = await db.Set<RolePermission>()
                .Where(p => p.Permission == Permissions.Api.Access && db.Set<Role>().Any(r => r.Id == p.RoleId && r.Name == BuiltInRoles.Itil))
                .SingleAsync();
            db.Remove(removed);
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        Assert.DoesNotContain(Permissions.Api.Access, await PermissionsOfAsync(BuiltInRoles.Itil));
    }

    [Fact]
    public async Task Administrator_role_always_keeps_the_wildcard()
    {
        await SeedAsync();
        await using (var db = new ServoraDbContext(_options))
        {
            var wildcard = await db.Set<RolePermission>()
                .Where(p => p.Permission == Permissions.All && db.Set<Role>().Any(r => r.Id == p.RoleId && r.Name == BuiltInRoles.Admin))
                .SingleAsync();
            db.Remove(wildcard);
            await db.SaveChangesAsync();
        }

        await SeedAsync();

        Assert.Contains(Permissions.All, await PermissionsOfAsync(BuiltInRoles.Admin));
    }

    [Fact]
    public async Task Seeding_is_idempotent()
    {
        await SeedAsync();
        await SeedAsync();

        await using var db = new ServoraDbContext(_options);
        var duplicates = await db.Set<RolePermission>()
            .GroupBy(p => new { p.RoleId, p.Permission })
            .Where(g => g.Count() > 1)
            .CountAsync();
        Assert.Equal(0, duplicates);
        Assert.Equal(BuiltInRoles.All.Count, await db.Set<Role>().CountAsync());
        Assert.Equal(1, await db.Set<User>().CountAsync(u => u.NormalizedUserName == "ADMIN"));
    }

    [Fact]
    public void Every_default_grant_is_a_known_permission()
    {
        Assert.All(BuiltInRoles.All.SelectMany(r => r.Permissions), p => Assert.True(PermissionCatalog.IsKnown(p), p));
    }

    private async Task SeedAsync()
    {
        await using var db = new ServoraDbContext(_options);
        await new IdentitySeeder(new PasswordHasherService(), NullLogger<IdentitySeeder>.Instance).SeedAsync(db, CancellationToken.None);
    }

    private async Task<List<string>> PermissionsOfAsync(string roleName)
    {
        await using var db = new ServoraDbContext(_options);
        return await db.Set<Role>().Where(r => r.Name == roleName).SelectMany(r => r.Permissions.Select(p => p.Permission)).ToListAsync();
    }
}
