using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Application.Identity.Admin;
using Servora.Application.Security;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Escalation guard and system-role protection of <see cref="RoleAdminService"/> (SQLite in-memory).</summary>
public sealed class RoleAdminServiceTests : IDisposable
{
    private readonly IdentityTestDatabase _db = new();
    private readonly RecordingAuditService _audit = new();

    [Fact]
    public async Task Create_WithPermissionTheActorLacks_IsForbiddenAndAudited()
    {
        var service = CreateService(new FakeCurrentUser(Permissions.Identity.ManageRoles, Permissions.Identity.ReadUsers, Permissions.Incidents.Read));

        var result = await service.CreateAsync(new RoleEditModel
        {
            Name = "incident_admin",
            Label = "Incident Admin",
            Permissions = [Permissions.Incidents.Read, Permissions.Incidents.Delete],
        });

        Assert.False(result.Succeeded);
        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
        Assert.Contains(Permissions.Incidents.Delete, result.Error, StringComparison.Ordinal);
        Assert.Contains(_audit.Entries, e => e.Action == "role.grant_denied");
        await using var db = await _db.CreateDbContextAsync();
        Assert.False(await db.Set<Role>().AnyAsync(r => r.Name == "incident_admin"));
    }

    [Fact]
    public async Task Create_WithHeldPermissions_Succeeds()
    {
        var service = CreateService(new FakeCurrentUser(Permissions.Identity.ManageRoles, "incident.*"));

        var result = await service.CreateAsync(new RoleEditModel
        {
            Name = "Incident_Admin",
            Label = "Incident Admin",
            Permissions = [Permissions.Incidents.Read, Permissions.Incidents.Delete, "incident.*"],
        });

        Assert.True(result.Succeeded, result.Error);
        await using var db = await _db.CreateDbContextAsync();
        var role = await db.Set<Role>().Include(r => r.Permissions).SingleAsync(r => r.Id == result.Value);
        Assert.Equal("incident_admin", role.Name);
        Assert.Equal(3, role.Permissions.Count);
    }

    [Fact]
    public async Task Create_RejectsUnknownPermissionKeys()
    {
        var service = CreateService(new FakeCurrentUser(Permissions.All));

        var result = await service.CreateAsync(new RoleEditModel { Name = "x_role", Label = "X role", Permissions = ["does.not.exist"] });

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_AddingPermissionTheActorLacks_IsForbidden_RemovingIsAllowed()
    {
        var roleId = await SeedRoleAsync("helper", false, Permissions.Incidents.Read, Permissions.Knowledge.Read);
        var service = CreateService(new FakeCurrentUser(Permissions.Identity.ManageRoles, Permissions.Identity.ReadUsers, Permissions.Incidents.Read));
        var version = (await service.GetAsync(roleId)).Value!.Version;

        var escalate = await service.UpdateAsync(roleId, new RoleEditModel { Label = "Helper", Permissions = [Permissions.Incidents.Read, Permissions.Knowledge.Read, Permissions.Configuration.ManageSecrets] }, version);
        Assert.Equal(ResultErrorKind.Forbidden, escalate.ErrorKind);

        // Knowledge.Read is not held by the actor, but removing it is not an escalation.
        var reduce = await service.UpdateAsync(roleId, new RoleEditModel { Label = "Helper", Permissions = [Permissions.Incidents.Read] }, version);
        Assert.True(reduce.Succeeded, reduce.Error);
    }

    [Fact]
    public async Task Update_AdministratorRoleMustKeepWildcard()
    {
        var roleId = await SeedRoleAsync("admin", true, Permissions.All);
        var service = CreateService(new FakeCurrentUser(Permissions.All));
        var version = (await service.GetAsync(roleId)).Value!.Version;

        var result = await service.UpdateAsync(roleId, new RoleEditModel { Name = "admin", Label = "System Administrator", Permissions = [Permissions.Incidents.Read] }, version);

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Update_SystemRoleCannotBeRenamed()
    {
        var roleId = await SeedRoleAsync("itil", true, Permissions.Incidents.Read);
        var service = CreateService(new FakeCurrentUser(Permissions.All));
        var version = (await service.GetAsync(roleId)).Value!.Version;

        var result = await service.UpdateAsync(roleId, new RoleEditModel { Name = "agents", Label = "ITIL", Permissions = [Permissions.Incidents.Read] }, version);

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
    }

    [Fact]
    public async Task Delete_SystemRoleIsRejected_CustomRoleIsDeleted()
    {
        var systemRole = await SeedRoleAsync("itil", true, Permissions.Incidents.Read);
        var customRole = await SeedRoleAsync("custom", false, Permissions.Incidents.Read);
        var service = CreateService(new FakeCurrentUser(Permissions.All));

        Assert.Equal(ResultErrorKind.Validation, (await service.DeleteAsync(systemRole)).ErrorKind);
        Assert.True((await service.DeleteAsync(customRole)).Succeeded);
    }

    [Fact]
    public async Task List_RequiresReadPermission()
    {
        var service = CreateService(new FakeCurrentUser(Permissions.Incidents.Read));

        Assert.Equal(ResultErrorKind.Forbidden, (await service.ListAsync()).ErrorKind);
    }

    /// <inheritdoc />
    public void Dispose() => _db.Dispose();

    private RoleAdminService CreateService(FakeCurrentUser user) =>
        new(user, _db, new PermissionService(_db, new MemoryCache(new MemoryCacheOptions())), _audit);

    private async Task<Guid> SeedRoleAsync(string name, bool system, params string[] permissions)
    {
        await using var db = await _db.CreateDbContextAsync();
        var role = new Role { Name = name, Label = name, IsSystem = system };
        foreach (var permission in permissions)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
        }

        db.Set<Role>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }
}
