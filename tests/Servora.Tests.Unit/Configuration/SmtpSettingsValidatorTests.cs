using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for SMTP option validation.</summary>
public sealed class SmtpSettingsValidatorTests
{
    private static SmtpSettingsUpdate Valid() => new()
    {
        Enabled = true,
        Host = "smtp.example.com",
        Port = 587,
        Security = SmtpSecurityMode.StartTls,
        UserName = "servora",
        NewPassword = "secret",
        FromAddress = "servora@example.com",
        FromName = "Servora",
        TimeoutSeconds = 30,
        MaxAttempts = 6,
    };

    [Fact]
    public void ValidConfiguration_HasNoErrors() => Assert.Empty(SmtpSettingsValidator.Validate(Valid()));

    [Fact]
    public void Enabled_RequiresHostAndSender()
    {
        var update = Valid();
        update.Host = " ";
        update.FromAddress = string.Empty;

        var errors = SmtpSettingsValidator.Validate(update);

        Assert.Equal(2, errors.Count);
    }

    [Fact]
    public void Disabled_AllowsEmptyConfiguration() =>
        Assert.Empty(SmtpSettingsValidator.Validate(new SmtpSettingsUpdate { Enabled = false, Host = string.Empty, FromAddress = string.Empty }));

    [Theory]
    [InlineData(0)]
    [InlineData(65536)]
    [InlineData(465)]
    public void Port_MustBeValidAndNotImplicitTls(int port)
    {
        var update = Valid();
        update.Port = port;

        Assert.Single(SmtpSettingsValidator.Validate(update));
    }

    [Fact]
    public void InvalidHostAndSender_AreRejected()
    {
        var update = Valid();
        update.Host = "smtp example com";
        update.FromAddress = "Servora <servora@example.com>";

        Assert.Equal(2, SmtpSettingsValidator.Validate(update).Count);
    }

    [Fact]
    public void Credentials_AreNotSentOverPlainConnections_ToRemoteHosts()
    {
        var update = Valid();
        update.Security = SmtpSecurityMode.None;

        Assert.Single(SmtpSettingsValidator.Validate(update));

        update.Host = "localhost";
        Assert.Empty(SmtpSettingsValidator.Validate(update));
    }

    [Fact]
    public void StoredPassword_CountsAsCredentials_UnlessCleared()
    {
        var update = Valid();
        update.Security = SmtpSecurityMode.None;
        update.NewPassword = null;

        Assert.Single(SmtpSettingsValidator.Validate(update, passwordStored: true));
        update.ClearPassword = true;
        Assert.Empty(SmtpSettingsValidator.Validate(update, passwordStored: true));
    }

    [Fact]
    public void HeaderInjection_InSenderNameOrUser_IsRejected()
    {
        var update = Valid();
        update.FromName = "Servora\r\nBcc: attacker@example.com";
        update.UserName = "user\nname";

        Assert.Equal(2, SmtpSettingsValidator.Validate(update).Count);
    }

    [Fact]
    public void TimeoutAttemptsAndPasswordLength_AreBounded()
    {
        var update = Valid();
        update.TimeoutSeconds = 2;
        update.MaxAttempts = 50;
        update.NewPassword = new string('x', SmtpSettingsValidator.MaxPasswordLength + 1);

        Assert.Equal(3, SmtpSettingsValidator.Validate(update).Count);
    }

    [Theory]
    [InlineData("a@example.com", true)]
    [InlineData("a@example.com\r\nRCPT TO:<b@example.com>", false)]
    [InlineData("", false)]
    public void IsValidAddress_RejectsInjection(string address, bool expected) =>
        Assert.Equal(expected, SmtpSettingsValidator.IsValidAddress(address));
}
