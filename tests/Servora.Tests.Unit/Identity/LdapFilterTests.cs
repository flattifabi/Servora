using Servora.Application.Identity.Ldap;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="LdapFilter"/> (RFC 4515 escaping and filter validation).</summary>
public sealed class LdapFilterTests
{
    [Theory]
    [InlineData("jane.doe", "jane.doe")]
    [InlineData("*", @"\2a")]
    [InlineData("(", @"\28")]
    [InlineData(")", @"\29")]
    [InlineData(@"\", @"\5c")]
    [InlineData("a\0b", @"a\00b")]
    [InlineData("Müller", "Müller")]
    [InlineData("", "")]
    public void Escape_EncodesSpecialCharacters(string input, string expected)
    {
        Assert.Equal(expected, LdapFilter.Escape(input));
    }

    [Fact]
    public void Escape_NeutralisesInjectionAttempt()
    {
        var escaped = LdapFilter.Escape("*)(uid=*))(|(uid=*");

        Assert.DoesNotContain('*', escaped);
        Assert.DoesNotContain('(', escaped);
        Assert.DoesNotContain(')', escaped);
        Assert.Equal(@"\2a\29\28uid=\2a\29\29\28|\28uid=\2a", escaped);
    }

    [Fact]
    public void Escape_EncodesControlCharacters()
    {
        Assert.Equal(@"a\0ab\0d", LdapFilter.Escape("a\nb\r"));
    }

    [Fact]
    public void EscapeBinary_EncodesEveryByte()
    {
        Assert.Equal(@"\01\05\ff", LdapFilter.EscapeBinary([0x01, 0x05, 0xff]));
    }

    [Fact]
    public void Equal_BuildsEscapedEqualityFilter()
    {
        Assert.Equal(@"(sAMAccountName=adm\2a)", LdapFilter.Equal("sAMAccountName", "adm*"));
    }

    [Theory]
    [InlineData("sAMAccountName(")]
    [InlineData("mail)(cn=*")]
    [InlineData("")]
    [InlineData("1bad")]
    public void Equal_RejectsInvalidAttributeNames(string attribute)
    {
        Assert.Throws<ArgumentException>(() => LdapFilter.Equal(attribute, "x"));
    }

    [Theory]
    [InlineData("objectGUID", true)]
    [InlineData("user-principal-name", true)]
    [InlineData("1.2.840.113556.1.4.1941", true)]
    [InlineData("mail;binary", false)]
    [InlineData("cn=", false)]
    [InlineData(null, false)]
    public void IsValidAttributeName_ValidatesDescriptors(string? attribute, bool expected)
    {
        Assert.Equal(expected, LdapFilter.IsValidAttributeName(attribute));
    }

    [Theory]
    [InlineData("(objectClass=group)", true)]
    [InlineData("(&(objectCategory=person)(objectClass=user))", true)]
    [InlineData(@"(cn=a\2ab)", true)]
    [InlineData("objectClass=group", false)]
    [InlineData("(objectClass=group", false)]
    [InlineData("(a=b))(c=d", false)]
    [InlineData("(a=b)(c=d)", false)]
    [InlineData(@"(cn=a\zz)", false)]
    [InlineData("(cn=a\nb)", false)]
    [InlineData("", false)]
    public void IsValidFilter_ChecksStructure(string filter, bool expected)
    {
        Assert.Equal(expected, LdapFilter.IsValidFilter(filter));
    }

    [Fact]
    public void BuildUserLookup_UsesAccountAttributeAndEscapesValue()
    {
        var settings = new DirectorySettings { UserFilter = "(objectClass=user)", UserNameAttribute = "sAMAccountName" };
        var login = DirectoryLoginName.Parse(@"CORP\*)(objectClass=*")!;

        var filter = LdapFilter.BuildUserLookup(settings, login);

        Assert.Equal(@"(&(objectClass=user)(sAMAccountName=\2a\29\28objectClass=\2a))", filter);
        Assert.True(LdapFilter.IsValidFilter(filter));
    }

    [Fact]
    public void BuildUserLookup_UsesUpnAttributeForUpnLogins()
    {
        var settings = new DirectorySettings { UserFilter = "(objectClass=user)", UpnAttribute = "userPrincipalName" };

        var filter = LdapFilter.BuildUserLookup(settings, DirectoryLoginName.Parse("jane@corp.example.com")!);

        Assert.Equal("(&(objectClass=user)(userPrincipalName=jane@corp.example.com))", filter);
    }
}
