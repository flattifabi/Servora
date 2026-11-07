using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Application.Identity.Admin;
using Servora.Application.Identity.Policies;
using Servora.Application.Identity;
using Servora.Application.Security;
using Servora.Domain.Identity;
using Servora.Infrastructure.Security;

namespace Servora.Tests.Unit.Identity;

/// <summary>Escalation guard on group membership / role grants and on managing more privileged users.</summary>
public sealed class GroupAndUserEscalationTests : IDisposable
{
    private readonly IdentityTestDatabase _db = new();
    private readonly RecordingAuditService _audit = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    [Fact]
    public async Task AddMember_ToGroupGrantingUnheldRole_IsForbidden()
    {
        var (groupId, userId) = await SeedGroupWithRoleAsync(Permissions.Configuration.ManageSecrets);
        var actor = new FakeCurrentUser(Permissions.Identity.ManageGroups, Permissions.Identity.ReadUsers);
        var service = new GroupAdminService(actor, _db, CreatePermissionService(), _audit);

        var result = await service.AddMembersAsync(groupId, [userId]);

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
        Assert.Contains(_audit.Entries, e => e.Action == "group.grant_denied");
        await using var db = await _db.CreateDbContextAsync();
        Assert.False(await db.Set<GroupMember>().AnyAsync(m => m.GroupId == groupId));
    }

    [Fact]
    public async Task AddMember_WhenActorHoldsGrantedPermissions_Succeeds()
    {
        var (groupId, userId) = await SeedGroupWithRoleAsync(Permissions.Incidents.Read);
        var actor = new FakeCurrentUser(Permissions.Identity.ManageGroups, Permissions.Incidents.Read);
        var service = new GroupAdminService(actor, _db, CreatePermissionService(), _audit);

        var result = await service.AddMembersAsync(groupId, [userId]);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(1, result.Value);
    }

    [Fact]
    public async Task GrantRoleToGroup_WithUnheldPermissions_IsForbidden()
    {
        var (groupId, _) = await SeedGroupWithRoleAsync(null);
        var roleId = await SeedRoleAsync(Permissions.All);
        var actor = new FakeCurrentUser(Permissions.Identity.ManageGroups, "admin.*");
        var service = new GroupAdminService(actor, _db, CreatePermissionService(), _audit);

        Assert.Equal(ResultErrorKind.Forbidden, (await service.AddRoleAsync(groupId, roleId)).ErrorKind);
    }

    [Fact]
    public async Task UserAdmin_CannotResetPasswordOfMorePrivilegedUser()
    {
        var adminRole = await SeedRoleAsync(Permissions.All);
        var targetId = await SeedUserAsync("root.admin", adminRole);
        var actor = new FakeCurrentUser(Permissions.Identity.ManageUsers, Permissions.Identity.ReadUsers);
        var service = CreateUserService(actor);

        var result = await service.ResetPasswordAsync(targetId, "Another-Strong-Pass-42");

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
    }

    [Fact]
    public async Task UserAdmin_CannotGrantRoleWithUnheldPermissions()
    {
        var adminRole = await SeedRoleAsync(Permissions.All);
        var targetId = await SeedUserAsync("jane.doe", null);
        var actor = new FakeCurrentUser(Permissions.Identity.ManageUsers, Permissions.Identity.ReadUsers);
        var service = CreateUserService(actor);

        var result = await service.AddRoleAsync(targetId, adminRole);

        Assert.Equal(ResultErrorKind.Forbidden, result.ErrorKind);
        Assert.Contains(_audit.Entries, e => e.Action == "user.role_grant_denied");
    }

    [Fact]
    public async Task UserAdmin_ResetPasswordSetsMustChangeAndRotatesStamp()
    {
        var targetId = await SeedUserAsync("jane.doe", null);
        string stampBefore;
        await using (var db = await _db.CreateDbContextAsync())
        {
            stampBefore = (await db.Set<User>().SingleAsync(u => u.Id == targetId)).SecurityStamp;
        }

        var invalidated = new List<Guid>();
        var service = CreateUserService(new FakeCurrentUser(Permissions.Identity.ManageUsers), new DelegateUserSessionInvalidator(invalidated.Add));

        var result = await service.ResetPasswordAsync(targetId, "Another-Strong-Pass-42");

        Assert.True(result.Succeeded, result.Error);
        await using var check = await _db.CreateDbContextAsync();
        var user = await check.Set<User>().SingleAsync(u => u.Id == targetId);
        Assert.True(user.MustChangePassword);
        Assert.NotEqual(stampBefore, user.SecurityStamp);
        Assert.Equal([targetId], invalidated);
    }

    [Fact]
    public async Task UserAdmin_CannotDeactivateLastAdministrator()
    {
        var adminRole = await SeedRoleAsync(Permissions.All);
        var targetId = await SeedUserAsync("only.admin", adminRole);
        var service = CreateUserService(new FakeCurrentUser(Permissions.All));

        var result = await service.SetActiveAsync(targetId, false);

        Assert.Equal(ResultErrorKind.Validation, result.ErrorKind);
        Assert.Equal(SuperAdminGuard.LastAdministratorMessage, result.Error);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cache.Dispose();
        _db.Dispose();
    }

    private PermissionService CreatePermissionService() => new(_db, _cache);

    private UserAdminService CreateUserService(FakeCurrentUser actor, params IUserSessionInvalidator[] invalidators) =>
        new(actor, _db, CreatePermissionService(), new PasswordHasherService(), new AuthPolicyProvider(new IdentitySettingsStore(_db, _cache)), _audit, new FixedClock(), invalidators);

    private async Task<Guid> SeedRoleAsync(params string[] permissions)
    {
        await using var db = await _db.CreateDbContextAsync();
        var role = new Role { Name = "role_" + Guid.NewGuid().ToString("N")[..8], Label = "Role" };
        foreach (var permission in permissions)
        {
            role.Permissions.Add(new RolePermission { RoleId = role.Id, Permission = permission });
        }

        db.Set<Role>().Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<Guid> SeedUserAsync(string userName, Guid? roleId)
    {
        await using var db = await _db.CreateDbContextAsync();
        var user = new User { UserName = userName, NormalizedUserName = userName.ToUpperInvariant(), DisplayName = userName, PasswordHash = "x" };
        if (roleId is { } id)
        {
            user.Roles.Add(new UserRole { UserId = user.Id, RoleId = id });
        }

        db.Set<User>().Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private async Task<(Guid GroupId, Guid UserId)> SeedGroupWithRoleAsync(string? permission)
    {
        var userId = await SeedUserAsync("member." + Guid.NewGuid().ToString("N")[..6], null);
        await using var db = await _db.CreateDbContextAsync();
        var group = new Group { Name = "Group " + Guid.NewGuid().ToString("N")[..6] };
        db.Set<Group>().Add(group);
        await db.SaveChangesAsync();
        if (permission is not null)
        {
            var roleId = await SeedRoleAsync(permission);
            db.Set<GroupRole>().Add(new GroupRole { GroupId = group.Id, RoleId = roleId });
            await db.SaveChangesAsync();
        }

        return (group.Id, userId);
    }
}
