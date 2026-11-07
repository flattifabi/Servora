using Servora.Application.Identity.Ldap;
using Servora.Domain.Identity;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="DirectoryRoleMapper"/> and <see cref="DistinguishedName"/> (group → role mapping).</summary>
public sealed class DirectoryRoleMapperTests
{
    private static readonly Guid s_adminRole = Guid.NewGuid();
    private static readonly Guid s_itilRole = Guid.NewGuid();
    private static readonly Guid s_serviceDesk = Guid.NewGuid();

    private static readonly string[] s_memberOf =
    [
        "CN=Servora Admins,OU=Groups,DC=corp,DC=example,DC=com",
        "CN=IT Service Desk,OU=Groups,DC=corp,DC=example,DC=com",
        @"CN=Smith\, Partners,OU=Groups,DC=corp,DC=example,DC=com",
    ];

    [Fact]
    public void Map_MatchesFullDistinguishedNameIgnoringCaseAndSpacing()
    {
        var mappings = new[] { Mapping("cn=servora admins, ou=groups, dc=corp, dc=example, dc=com", role: s_adminRole) };

        var result = DirectoryRoleMapper.Map(s_memberOf, mappings);

        Assert.Equal([s_adminRole], result.RoleIds);
        Assert.Empty(result.GroupIds);
    }

    [Fact]
    public void Map_MatchesCommonName()
    {
        var mappings = new[] { Mapping("it service desk", role: s_itilRole, group: s_serviceDesk) };

        var result = DirectoryRoleMapper.Map(s_memberOf, mappings);

        Assert.Equal([s_itilRole], result.RoleIds);
        Assert.Equal([s_serviceDesk], result.GroupIds);
    }

    [Fact]
    public void Map_HandlesEscapedCommaInCommonName()
    {
        var mappings = new[] { Mapping("Smith, Partners", role: s_itilRole) };

        Assert.Equal([s_itilRole], DirectoryRoleMapper.Map(s_memberOf, mappings).RoleIds);
    }

    [Fact]
    public void Map_IgnoresInactiveAndUnmatchedMappings()
    {
        var mappings = new[]
        {
            Mapping("Servora Admins", role: s_adminRole, active: false),
            Mapping("CN=Servora Admins,OU=Other,DC=corp,DC=example,DC=com", role: s_adminRole),
            Mapping("Finance", role: s_itilRole),
        };

        var result = DirectoryRoleMapper.Map(s_memberOf, mappings);

        Assert.Empty(result.RoleIds);
        Assert.Empty(result.GroupIds);
    }

    [Fact]
    public void Map_DoesNotTreatCommonNameAsSubstring()
    {
        var mappings = new[] { Mapping("Admins", role: s_adminRole) };

        Assert.Empty(DirectoryRoleMapper.Map(s_memberOf, mappings).RoleIds);
    }

    [Fact]
    public void Map_CombinesSeveralMappings()
    {
        var mappings = new[]
        {
            Mapping("Servora Admins", role: s_adminRole),
            Mapping("CN=IT Service Desk,OU=Groups,DC=corp,DC=example,DC=com", role: s_itilRole, group: s_serviceDesk),
        };

        var result = DirectoryRoleMapper.Map(s_memberOf, mappings);

        Assert.Equal(2, result.RoleIds.Count);
        Assert.Contains(s_adminRole, result.RoleIds);
        Assert.Contains(s_itilRole, result.RoleIds);
        Assert.Equal([s_serviceDesk], result.GroupIds);
    }

    [Fact]
    public void Map_WithoutGroupsGrantsNothing()
    {
        var result = DirectoryRoleMapper.Map([], [Mapping("Servora Admins", role: s_adminRole)]);

        Assert.Empty(result.RoleIds);
    }

    [Fact]
    public void DistinguishedName_NormalizeAndLeafValue()
    {
        Assert.Equal("cn=a b,ou=x,dc=corp", DistinguishedName.Normalize(" CN = A B , OU=X;DC=Corp "));
        Assert.Equal("Smith, Partners", DistinguishedName.GetLeafValue(@"CN=Smith\, Partners,OU=Groups"));
        Assert.Equal("Müller", DistinguishedName.GetLeafValue(@"CN=M\c3\bcller,OU=Groups"));
        Assert.True(DistinguishedName.AreEqual("CN=A,DC=x", "cn=a, dc=X"));
        Assert.False(DistinguishedName.AreEqual("", ""));
    }

    private static DirectoryGroupMapping Mapping(string directoryGroup, Guid? role = null, Guid? group = null, bool active = true) =>
        new() { DirectoryGroup = directoryGroup, RoleId = role, GroupId = group, IsActive = active };
}
