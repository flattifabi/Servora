using Servora.Application.Identity;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="PrivilegeEscalationGuard"/>.</summary>
public sealed class PrivilegeEscalationGuardTests
{
    [Fact]
    public void FindUngranted_AllowsPermissionsTheActorHolds()
    {
        var actor = Set("incident.read", "incident.write");

        Assert.Empty(PrivilegeEscalationGuard.FindUngranted(actor, ["incident.read", "incident.write"]));
    }

    [Fact]
    public void FindUngranted_ReportsPermissionsTheActorLacks()
    {
        var actor = Set("incident.read", Permissions.Identity.ManageRoles);

        var missing = PrivilegeEscalationGuard.FindUngranted(actor, ["incident.read", "incident.delete", "admin.secrets.manage"]);

        Assert.Equal(["incident.delete", "admin.secrets.manage"], missing);
    }

    [Fact]
    public void FindUngranted_GlobalWildcardOnlyCoveredByWildcard()
    {
        var almostEverything = Set(PermissionCatalog.All.Select(p => p.Key).ToArray());

        Assert.Equal([Permissions.All], PrivilegeEscalationGuard.FindUngranted(almostEverything, [Permissions.All]));
        Assert.Empty(PrivilegeEscalationGuard.FindUngranted(Set(Permissions.All), [Permissions.All, "incident.delete", "table.x.read"]));
    }

    [Fact]
    public void FindUngranted_PrefixWildcardRequiresSameOrBroaderWildcard()
    {
        var actor = Set("incident.read", "incident.write", "incident.create", "incident.delete");

        // Holding every individual key does not allow granting the wildcard (it would also cover future keys).
        Assert.Equal(["incident.*"], PrivilegeEscalationGuard.FindUngranted(actor, ["incident.*"]));
        Assert.Empty(PrivilegeEscalationGuard.FindUngranted(Set("incident.*"), ["incident.*", "incident.delete"]));
        Assert.Empty(PrivilegeEscalationGuard.FindUngranted(Set("admin.*"), ["admin.users.*", "admin.users.manage"]));
    }

    [Fact]
    public void FindUngranted_IgnoresBlankAndDuplicateKeys()
    {
        var missing = PrivilegeEscalationGuard.FindUngranted(Set(), ["x.y", "x.y", " ", string.Empty]);

        Assert.Equal(["x.y"], missing);
    }

    [Fact]
    public async Task FindUngrantedAsync_UsesThePermissionCheckDelegate()
    {
        var checkedKeys = new List<string>();
        Task<bool> Holds(string key, CancellationToken _)
        {
            checkedKeys.Add(key);
            return Task.FromResult(key.StartsWith("incident.", StringComparison.Ordinal));
        }

        var missing = await PrivilegeEscalationGuard.FindUngrantedAsync(Holds, ["incident.read", "change.approve", "incident.read"]);

        Assert.Equal(["change.approve"], missing);
        Assert.Equal(["incident.read", "change.approve"], checkedKeys);
    }

    [Fact]
    public void Describe_ListsMissingKeys()
    {
        var message = PrivilegeEscalationGuard.Describe(["a.b", "c.d", "e.f", "g.h", "i.j", "k.l", "m.n"]);

        Assert.Contains("a.b", message, StringComparison.Ordinal);
        Assert.Contains("and 2 more", message, StringComparison.Ordinal);
    }

    private static HashSet<string> Set(params string[] keys) => new(keys, StringComparer.Ordinal);
}
