using Servora.Application.Api;
using Servora.Application.Common;
using Servora.Application.Security;

namespace Servora.Tests.Unit.Api;

/// <summary>Tests for <see cref="ApiKeyScopePolicy"/> (no privilege escalation through keys).</summary>
public sealed class ApiKeyScopePolicyTests
{
    [Fact]
    public void IsGrantable_RequiresTheCreatorToHoldTheScope()
    {
        var creator = new HashSet<string> { Permissions.Incidents.Read, Permissions.Api.Access };

        Assert.True(ApiKeyScopePolicy.IsGrantable(creator, Permissions.Incidents.Read));
        Assert.False(ApiKeyScopePolicy.IsGrantable(creator, Permissions.Incidents.Create));
        Assert.False(ApiKeyScopePolicy.IsGrantable(creator, Permissions.All));
    }

    [Fact]
    public void IsGrantable_AdminMayGrantKnownScopesOnly()
    {
        var admin = new HashSet<string> { Permissions.All };

        Assert.True(ApiKeyScopePolicy.IsGrantable(admin, Permissions.Incidents.Create));
        Assert.False(ApiKeyScopePolicy.IsGrantable(admin, "made.up.permission"));
        Assert.False(ApiKeyScopePolicy.IsGrantable(admin, Permissions.Incidents.Read.ToUpperInvariant()));
        Assert.False(ApiKeyScopePolicy.IsGrantable(admin, string.Empty));
    }

    [Fact]
    public void IsGrantable_ToolScopesRequireMcpAccess()
    {
        var withMcp = new HashSet<string> { Permissions.Mcp.Access };
        var withoutMcp = new HashSet<string> { Permissions.Incidents.Read };

        Assert.True(ApiKeyScopePolicy.IsGrantable(withMcp, "mcp.tool.search_records"));
        Assert.True(ApiKeyScopePolicy.IsGrantable(withMcp, "mcp.tool.*"));
        Assert.False(ApiKeyScopePolicy.IsGrantable(withMcp, "mcp.tool.Bad-Name"));
        Assert.False(ApiKeyScopePolicy.IsGrantable(withoutMcp, "mcp.tool.search_records"));
    }

    [Fact]
    public void Validate_NormalizesAndRejectsEscalation()
    {
        var creator = new HashSet<string> { Permissions.Incidents.Read, Permissions.Api.Access };

        var ok = ApiKeyScopePolicy.Validate(creator, [Permissions.Incidents.Read, $" {Permissions.Api.Access} ", Permissions.Incidents.Read]);
        Assert.True(ok.Succeeded, ok.Error);
        Assert.Equal([Permissions.Api.Access, Permissions.Incidents.Read], ok.Value);

        var escalation = ApiKeyScopePolicy.Validate(creator, [Permissions.Incidents.Read, Permissions.Incidents.Create]);
        Assert.Equal(ResultErrorKind.Forbidden, escalation.ErrorKind);
        Assert.Contains(Permissions.Incidents.Create, escalation.Error);

        Assert.Equal(ResultErrorKind.Validation, ApiKeyScopePolicy.Validate(creator, []).ErrorKind);
    }

    [Fact]
    public void ParseAndFormat_RoundTrip()
    {
        var parsed = ApiKeyScopePolicy.Parse("b.read, a.read;c.read\nb.read");

        Assert.Equal(["a.read", "b.read", "c.read"], parsed);
        Assert.Equal("a.read b.read c.read", ApiKeyScopePolicy.Format(parsed));
        Assert.Empty(ApiKeyScopePolicy.Parse("  "));
    }
}
