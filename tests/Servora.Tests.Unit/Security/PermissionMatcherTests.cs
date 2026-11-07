using Servora.Application.Security;

namespace Servora.Tests.Unit.Security;

/// <summary>Tests for <see cref="PermissionMatcher"/>.</summary>
public sealed class PermissionMatcherTests
{
    [Theory]
    [InlineData("*", "incident.write", true)]
    [InlineData("incident.write", "incident.write", true)]
    [InlineData("incident.*", "incident.write", true)]
    [InlineData("admin.*", "admin.users.manage", true)]
    [InlineData("incident.read", "incident.write", false)]
    [InlineData("incident.*", "problem.read", false)]
    [InlineData("inc.*", "incident.read", false)]
    public void IsGranted_EvaluatesExactAndWildcardPermissions(string granted, string required, bool expected)
    {
        var set = new HashSet<string>(StringComparer.Ordinal) { granted };
        Assert.Equal(expected, PermissionMatcher.IsGranted(set, required));
    }

    [Fact]
    public void IsGranted_EmptySet_DeniesByDefault()
    {
        Assert.False(PermissionMatcher.IsGranted(new HashSet<string>(), "incident.read"));
    }

    [Fact]
    public void Catalog_ContainsUniqueKeys()
    {
        var keys = PermissionCatalog.All.Select(p => p.Key).ToList();
        Assert.NotEmpty(keys);
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
    }
}
