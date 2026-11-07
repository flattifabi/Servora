using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Identity;
using Servora.Application.Identity.Ldap;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Just-in-time provisioning and group → role reconciliation of <see cref="DirectoryProvisioningService"/>.</summary>
public sealed class DirectoryProvisioningServiceTests : IDisposable
{
    private const string AdminsDn = "CN=Servora Admins,OU=Groups,DC=corp,DC=example,DC=com";
    private const string DeskDn = "CN=IT Service Desk,OU=Groups,DC=corp,DC=example,DC=com";

    private readonly IdentityTestDatabase _db = new();
    private readonly RecordingAuditService _audit = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly DirectorySettings _settings = new() { DefaultRoleName = "user" };

    [Fact]
    public async Task Provision_CreatesUserWithDefaultAndMappedRolesAndGroups()
    {
        var (userRole, itilRole, deskGroup, mappings) = await SeedAsync();

        var result = await CreateService().ProvisionAsync(Entry("jane.doe", [DeskDn]), _settings, mappings, allowCreate: true);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Created);
        await using var db = await _db.CreateDbContextAsync();
        var user = await db.Set<User>().Include(u => u.Roles).Include(u => u.Groups).SingleAsync(u => u.UserName == "jane.doe");
        Assert.Equal(UserSource.Directory, user.Source);
        Assert.Equal("guid-jane.doe", user.ExternalId);
        Assert.Equal("Jane Doe", user.DisplayName);
        Assert.Null(user.PasswordHash);
        Assert.Contains(user.Roles, r => r.RoleId == userRole && !r.IsFromDirectory);
        Assert.Contains(user.Roles, r => r.RoleId == itilRole && r.IsFromDirectory);
        Assert.Contains(user.Groups, m => m.GroupId == deskGroup && m.IsFromDirectory);
        Assert.Contains(_audit.Entries, e => e.Action == "user.provisioned");
    }

    [Fact]
    public async Task Provision_WithoutCreatePermission_SkipsUnknownUsers()
    {
        var (_, _, _, mappings) = await SeedAsync();

        var result = await CreateService().ProvisionAsync(Entry("new.user", []), _settings, mappings, allowCreate: false);

        Assert.True(result.Skipped);
        Assert.Null(result.User);
    }

    [Fact]
    public async Task Provision_RemovesStaleDirectoryRolesButKeepsManualOnes()
    {
        var (_, itilRole, deskGroup, mappings) = await SeedAsync();
        var adminRole = await AddRoleAsync("admin_like", Permissions.All);
        var service = CreateService();
        await service.ProvisionAsync(Entry("jane.doe", [DeskDn]), _settings, mappings, allowCreate: true);

        // An administrator grants a manual role; afterwards the user leaves the service desk group in AD.
        await using (var db = await _db.CreateDbContextAsync())
        {
            var user = await db.Set<User>().SingleAsync(u => u.UserName == "jane.doe");
            db.Set<UserRole>().Add(new UserRole { UserId = user.Id, RoleId = adminRole });
            await db.SaveChangesAsync();
        }

        var result = await service.ProvisionAsync(Entry("jane.doe", []), _settings, mappings, allowCreate: true);

        Assert.True(result.Succeeded);
        Assert.Equal(2, result.MembershipChanges);
        await using var check = await _db.CreateDbContextAsync();
        var reloaded = await check.Set<User>().Include(u => u.Roles).Include(u => u.Groups).SingleAsync(u => u.UserName == "jane.doe");
        Assert.DoesNotContain(reloaded.Roles, r => r.RoleId == itilRole);
        Assert.Contains(reloaded.Roles, r => r.RoleId == adminRole && !r.IsFromDirectory);
        Assert.DoesNotContain(reloaded.Groups, m => m.GroupId == deskGroup);
    }

    [Fact]
    public async Task Provision_NeverLinksAnExistingLocalAccount()
    {
        var (_, _, _, mappings) = await SeedAsync();
        await using (var db = await _db.CreateDbContextAsync())
        {
            db.Set<User>().Add(new User { UserName = "admin", NormalizedUserName = "ADMIN", DisplayName = "Local admin", Source = UserSource.Local, PasswordHash = "hash" });
            await db.SaveChangesAsync();
        }

        var result = await CreateService().ProvisionAsync(Entry("admin", [AdminsDn]), _settings, mappings, allowCreate: true);

        Assert.False(result.Succeeded);
        Assert.NotNull(result.Error);
        Assert.Contains(_audit.Entries, e => e.Action == "directory.link_refused");
        await using var check = await _db.CreateDbContextAsync();
        var local = await check.Set<User>().Include(u => u.Roles).SingleAsync(u => u.NormalizedUserName == "ADMIN");
        Assert.Equal(UserSource.Local, local.Source);
        Assert.Empty(local.Roles);
    }

    [Fact]
    public async Task Provision_DisabledDirectoryAccountDeactivatesUser_AndNeverReactivates()
    {
        var (_, _, _, mappings) = await SeedAsync();
        var invalidated = new List<Guid>();
        var service = CreateService(new DelegateUserSessionInvalidator(invalidated.Add));
        var created = await service.ProvisionAsync(Entry("jane.doe", []), _settings, mappings, allowCreate: true);

        var disabled = await service.ProvisionAsync(Entry("jane.doe", [], disabled: true), _settings, mappings, allowCreate: true);
        var enabledAgain = await service.ProvisionAsync(Entry("jane.doe", []), _settings, mappings, allowCreate: true);

        Assert.True(disabled.Deactivated);
        Assert.Equal([created.User!.Id], invalidated);
        Assert.False(enabledAgain.User!.IsActive);
    }

    [Fact]
    public async Task Provision_MatchesByExternalIdAfterRename()
    {
        var (_, _, _, mappings) = await SeedAsync();
        var service = CreateService();
        var first = await service.ProvisionAsync(Entry("jane.doe", []), _settings, mappings, allowCreate: true);

        var renamed = Entry("jane.doe", []) with { UserName = "jane.smith", LastName = "Smith", DisplayName = "Jane Smith" };
        var second = await service.ProvisionAsync(renamed, _settings, mappings, allowCreate: true);

        Assert.Equal(first.User!.Id, second.User!.Id);
        Assert.Equal("Jane Smith", second.User.DisplayName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

    private static DirectoryUserEntry Entry(string userName, IReadOnlyList<string> groups, bool disabled = false) => new(
        $"CN={userName},OU=Users,DC=corp,DC=example,DC=com",
        userName,
        "Jane Doe",
        "Jane",
        "Doe",
        $"{userName}@corp.example.com",
        null,
        "Analyst",
        "IT",
        $"guid-{userName}",
        null,
        disabled,
        groups);

    private DirectoryProvisioningService CreateService(params IUserSessionInvalidator[] invalidators) =>
        new(_db, new PermissionService(_db, _cache), _audit, new FixedClock(), invalidators);

    private async Task<Guid> AddRoleAsync(string name, params string[] permissions)
    {
        await using var db = await _db.CreateDbContextAsync();
        var role = new Role { Name = name, Label = name };
        foreach (var permission in permissions)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
        }

        db.Set<Role>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<(Guid UserRole, Guid ItilRole, Guid DeskGroup, IReadOnlyList<DirectoryGroupMapping> Mappings)> SeedAsync()
    {
        var userRole = await AddRoleAsync("user", Permissions.Portal.Access);
        var itilRole = await AddRoleAsync("itil", Permissions.Incidents.Read);
        await using var db = await _db.CreateDbContextAsync();
        var desk = new Group { Name = "Service Desk" };
        db.Set<Group>().Add(desk);
        var mappings = new List<DirectoryGroupMapping>
        {
            new() { DirectoryGroup = "IT Service Desk", RoleId = itilRole, GroupId = desk.Id },
        };
        db.Set<DirectoryGroupMapping>().AddRange(mappings);
        await db.SaveChangesAsync();
        return (userRole, itilRole, desk.Id, mappings);
    }
}
