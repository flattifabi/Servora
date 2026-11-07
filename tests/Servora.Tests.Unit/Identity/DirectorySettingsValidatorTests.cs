using Servora.Application.Identity.Ldap;

namespace Servora.Tests.Unit.Identity;

/// <summary>Tests for <see cref="DirectorySettingsValidator"/> and connection-target change detection.</summary>
public sealed class DirectorySettingsValidatorTests
{
    private static DirectorySettings Valid() => new()
    {
        Enabled = true,
        Host = "dc01.corp.example.com",
        Port = 636,
        BaseDn = "DC=corp,DC=example,DC=com",
        BindDn = "CN=svc,DC=corp,DC=example,DC=com",
    };

    [Fact]
    public void Validate_AcceptsCompleteSettings()
    {
        Assert.Null(DirectorySettingsValidator.Validate(Valid()));
    }

    [Fact]
    public void Validate_DefaultDisabledSettingsAreValid()
    {
        Assert.Null(DirectorySettingsValidator.Validate(new DirectorySettings()));
    }

    [Theory]
    [InlineData("ldaps://dc01")]
    [InlineData("dc01/path")]
    [InlineData("dc 01")]
    [InlineData("")]
    public void Validate_RejectsInvalidHost(string host)
    {
        Assert.NotNull(DirectorySettingsValidator.Validate(Valid() with { Host = host }));
    }

    [Fact]
    public void Validate_RequiresServiceAccount()
    {
        Assert.NotNull(DirectorySettingsValidator.Validate(Valid() with { BindDn = " " }));
    }

    [Fact]
    public void Validate_RejectsMalformedFiltersAndAttributes()
    {
        Assert.NotNull(DirectorySettingsValidator.Validate(Valid() with { UserFilter = "objectClass=user" }));
        Assert.NotNull(DirectorySettingsValidator.Validate(Valid() with { GroupFilter = "(objectClass=group))(" }));
        Assert.NotNull(DirectorySettingsValidator.Validate(Valid() with { UserNameAttribute = "sAMAccountName)(cn=*" }));
    }

    [Fact]
    public void HasDifferentConnectionTarget_DetectsServerOrAccountChanges()
    {
        var saved = Valid();

        Assert.False(saved.HasDifferentConnectionTarget(saved with { TimeoutSeconds = 30, Host = "DC01.corp.example.com " }));
        Assert.True(saved.HasDifferentConnectionTarget(saved with { Host = "evil.example.net" }));
        Assert.True(saved.HasDifferentConnectionTarget(saved with { Port = 389 }));
        Assert.True(saved.HasDifferentConnectionTarget(saved with { Security = LdapSecurityMode.None }));
        Assert.True(saved.HasDifferentConnectionTarget(saved with { BindDn = "CN=other,DC=corp" }));
    }
}
