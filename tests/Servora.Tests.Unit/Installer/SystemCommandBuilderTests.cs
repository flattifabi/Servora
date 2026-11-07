using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for the argument lists passed to <c>sc.exe</c>, <c>netsh.exe</c> and <c>icacls.exe</c>.</summary>
public sealed class SystemCommandBuilderTests
{
    [Fact]
    public void ScCreate_QuotesPathAndUsesDelayedAutoStart()
    {
        var definition = new ServiceDefinition("Servora", "Servora", "desc", @"C:\Program Files\Servora\Servora.exe", ServiceAccountKind.Virtual, new Dictionary<string, string>());

        var args = ScCommandBuilder.Create(definition);

        Assert.Equal(
            ["create", "Servora", "binPath=", "\"C:\\Program Files\\Servora\\Servora.exe\"", "start=", "delayed-auto", "obj=", @"NT SERVICE\Servora", "DisplayName=", "Servora"],
            args);
    }

    [Theory]
    [InlineData(nameof(ServiceAccountKind.Virtual), @"NT SERVICE\Servora")]
    [InlineData(nameof(ServiceAccountKind.LocalSystem), "LocalSystem")]
    [InlineData(nameof(ServiceAccountKind.NetworkService), @"NT AUTHORITY\NetworkService")]
    public void AccountName_MapsKinds(string kind, string expected)
    {
        Assert.Equal(expected, ScCommandBuilder.AccountName(Enum.Parse<ServiceAccountKind>(kind), "Servora"));
    }

    [Fact]
    public void ScRecovery_RestartsOnFailure()
    {
        Assert.Equal(["failure", "Servora", "reset=", "86400", "actions=", "restart/60000/restart/60000/restart/300000"], ScCommandBuilder.Failure("Servora"));
        Assert.Equal(["failureflag", "Servora", "1"], ScCommandBuilder.FailureFlag("Servora"));
        Assert.Equal(["sidtype", "Servora", "unrestricted"], ScCommandBuilder.SidType("Servora"));
        Assert.Equal(["delete", "Servora"], ScCommandBuilder.Delete("Servora"));
    }

    [Fact]
    public void Firewall_OpensOnlyTheTcpPortOnTrustedProfiles()
    {
        var args = FirewallManager.AddRuleArguments("Servora", 5080);

        Assert.Equal(["advfirewall", "firewall", "add", "rule", "name=Servora", "dir=in", "action=allow", "protocol=TCP", "localport=5080", "profile=domain,private", "enable=yes"], args);
        Assert.Equal(["advfirewall", "firewall", "delete", "rule", "name=Servora"], FirewallManager.DeleteRuleArguments("Servora"));
    }

    [Fact]
    public void FirewallAddRule_ReplacesExistingRuleAndReportsFailures()
    {
        var processes = new FakeProcessRunner();
        var firewall = new FirewallManager(processes, new FakeInstallerEnvironment());

        firewall.AddRule("Servora", 5080);

        Assert.Equal(["delete", "add"], processes.Runs.Select(r => r.Arguments[2]));
        Assert.All(processes.Runs, r => Assert.Equal(@"C:\Windows\System32\netsh.exe", r.FileName));

        processes.Handler = (_, args) => new ProcessResult(args[2] == "add" ? 1 : 0, "The requested operation requires elevation.");
        Assert.Throws<InstallerException>(() => firewall.AddRule("Servora", 5080));
    }

    [Fact]
    public void Icacls_InstallDirectory_RemovesInheritanceAndGrantsReadToUsers()
    {
        var args = AccessControlManager.InstallDirectoryArguments(@"C:\Program Files\Servora");

        Assert.Equal([@"C:\Program Files\Servora", "/inheritance:r", "/grant:r", "*S-1-5-18:(OI)(CI)F", "*S-1-5-32-544:(OI)(CI)F", "*S-1-5-32-545:(OI)(CI)RX", "/Q"], args);
    }

    [Theory]
    [InlineData(nameof(ServiceAccountKind.Virtual), @"NT SERVICE\Servora:(OI)(CI)M")]
    [InlineData(nameof(ServiceAccountKind.NetworkService), "*S-1-5-20:(OI)(CI)M")]
    [InlineData(nameof(ServiceAccountKind.LocalSystem), null)]
    public void Icacls_DataDirectory_GrantsOnlySystemAdminsAndServiceAccount(string account, string? serviceGrant)
    {
        var args = AccessControlManager.DataDirectoryArguments(@"C:\ProgramData\Servora", Enum.Parse<ServiceAccountKind>(account), "Servora");

        Assert.Equal(@"C:\ProgramData\Servora", args[0]);
        Assert.Contains("/inheritance:r", args);
        Assert.Contains("*S-1-5-18:(OI)(CI)F", args);
        Assert.Contains("*S-1-5-32-544:(OI)(CI)F", args);
        Assert.DoesNotContain(args, a => a.StartsWith("*S-1-5-32-545", StringComparison.Ordinal));
        if (serviceGrant is null)
        {
            Assert.Equal(6, args.Count);
        }
        else
        {
            Assert.Contains(serviceGrant, args);
        }
    }
}
