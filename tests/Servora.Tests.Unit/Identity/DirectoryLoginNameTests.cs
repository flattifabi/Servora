using Servora.Application.Identity.Ldap;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="DirectoryLoginName"/>.</summary>
public sealed class DirectoryLoginNameTests
{
    [Fact]
    public void Parse_PlainAccount()
    {
        var login = DirectoryLoginName.Parse(" jane.doe ");

        Assert.NotNull(login);
        Assert.Equal("jane.doe", login.Account);
        Assert.Null(login.Domain);
        Assert.Null(login.UserPrincipalName);
    }

    [Fact]
    public void Parse_DomainAccount()
    {
        var login = DirectoryLoginName.Parse(@"CORP\jane.doe");

        Assert.NotNull(login);
        Assert.Equal("jane.doe", login.Account);
        Assert.Equal("CORP", login.Domain);
    }

    [Fact]
    public void Parse_UserPrincipalName()
    {
        var login = DirectoryLoginName.Parse("jane.doe@corp.example.com");

        Assert.NotNull(login);
        Assert.Equal("jane.doe", login.Account);
        Assert.Equal("jane.doe@corp.example.com", login.UserPrincipalName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(@"A\B\c")]
    [InlineData(@"\jane")]
    [InlineData(@"CORP\")]
    [InlineData("a@b@c")]
    [InlineData("@corp")]
    [InlineData(@"CORP\jane@corp")]
    [InlineData("jane\u0000doe")]
    public void Parse_RejectsMalformedInput(string input)
    {
        Assert.Null(DirectoryLoginName.Parse(input));
    }

    [Theory]
    [InlineData(@"CORP\jane", "corp", true)]
    [InlineData(@"OTHER\jane", "CORP", false)]
    [InlineData("jane", "CORP", true)]
    [InlineData(@"OTHER\jane", null, true)]
    public void MatchesDomain_ComparesNetBiosDomain(string input, string? configured, bool expected)
    {
        Assert.Equal(expected, DirectoryLoginName.Parse(input)!.MatchesDomain(configured));
    }
}
