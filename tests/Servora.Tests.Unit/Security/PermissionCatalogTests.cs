using Servora.Application.Security;
using Servora.Application.Tables;

namespace Servora.Tests.Unit.Security;

/// <summary><see cref="PermissionCatalog.IsKnown"/> accepts exactly the keys that are checked at runtime.</summary>
public sealed class PermissionCatalogTests
{
    [Theory]
    [InlineData("*")]
    [InlineData("incident.read")]
    [InlineData("incident.*")]
    [InlineData("admin.*")]
    [InlineData("table.*")]
    [InlineData("table.incident.*")]
    [InlineData("table.incident.read")]
    [InlineData("table.u_hr_case.delete")]
    [InlineData("mcp.tool.*")]
    [InlineData("mcp.tool.search_records")]
    public void Known_keys(string key) => Assert.True(PermissionCatalog.IsKnown(key), key);

    [Theory]
    [InlineData("")]
    [InlineData("incident.fly")]
    [InlineData("nothing.*")]
    [InlineData("table.")]
    [InlineData("table.incident")]
    [InlineData("table.incident.drop")]
    [InlineData("table.Incident.read")]
    [InlineData("table..read")]
    [InlineData("table.incident.read.extra")]
    [InlineData("mcp.tool.")]
    [InlineData("mcp.tool.Bad-Name")]
    public void Unknown_keys(string key) => Assert.False(PermissionCatalog.IsKnown(key), key);

    [Theory]
    [InlineData(TableOperation.Read)]
    [InlineData(TableOperation.Create)]
    [InlineData(TableOperation.Write)]
    [InlineData(TableOperation.Delete)]
    public void Every_generic_table_permission_checked_at_runtime_is_known(TableOperation operation)
    {
        var key = TablePolicies.GenericPermission("cmdb_ci", operation);

        Assert.True(PermissionCatalog.IsKnown(key), key);

        // The wildcards an administrator would grant also cover the runtime key.
        Assert.True(PermissionMatcher.IsGranted(new HashSet<string> { "table.*" }, key));
        Assert.True(PermissionMatcher.IsGranted(new HashSet<string> { "table.cmdb_ci.*" }, key));
    }
}
