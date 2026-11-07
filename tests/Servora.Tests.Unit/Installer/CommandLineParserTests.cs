using Servora.Installer.Cli;
using Servora.Installer.Model;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="CommandLineParser"/>.</summary>
public sealed class CommandLineParserTests
{
    [Fact]
    public void NoArguments_SelectsAutoMode()
    {
        var result = CommandLineParser.Parse([]);

        Assert.True(result.IsValid);
        Assert.Equal(InstallerCommand.Auto, result.Options.Command);
        Assert.False(result.Options.Silent);
        Assert.Equal(ServiceAccountKind.Virtual, result.Options.ServiceAccount);
        Assert.Equal(5, result.Options.KeepBackups);
    }

    [Fact]
    public void InstallWithAllOptions_IsParsed()
    {
        var result = CommandLineParser.Parse(
        [
            "install", "--silent", "--port", "8080", "--install-dir", @"D:\Servora", "--data-dir=D:\\Data",
            "--firewall", "--no-browser", "--service-account", "LocalSystem", "--health-timeout", "300",
            "--health-url", "https://servora.example.com/health", "--source", @"C:\drop\Servora-1.0.0-win-x64.zip", "--force",
        ]);

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var options = result.Options;
        Assert.Equal(InstallerCommand.Install, options.Command);
        Assert.True(options.Silent);
        Assert.Equal(8080, options.Port);
        Assert.Equal(@"D:\Servora", options.InstallDirectory);
        Assert.Equal(@"D:\Data", options.DataDirectory);
        Assert.True(options.Firewall);
        Assert.True(options.NoBrowser);
        Assert.True(options.Force);
        Assert.Equal(ServiceAccountKind.LocalSystem, options.ServiceAccount);
        Assert.Equal(TimeSpan.FromSeconds(300), options.HealthTimeout);
        Assert.Equal("https://servora.example.com/health", options.HealthUrl!.ToString());
        Assert.Equal(@"C:\drop\Servora-1.0.0-win-x64.zip", options.Source);
    }

    [Theory]
    [InlineData("update", nameof(InstallerCommand.Update))]
    [InlineData("UPGRADE", nameof(InstallerCommand.Update))]
    [InlineData("uninstall", nameof(InstallerCommand.Uninstall))]
    [InlineData("status", nameof(InstallerCommand.Status))]
    [InlineData("--help", nameof(InstallerCommand.Help))]
    [InlineData("/?", nameof(InstallerCommand.Help))]
    [InlineData("-h", nameof(InstallerCommand.Help))]
    [InlineData("--version", nameof(InstallerCommand.Version))]
    public void Commands_AreRecognised(string token, string expected)
    {
        Assert.Equal(Enum.Parse<InstallerCommand>(expected), CommandLineParser.Parse([token]).Options.Command);
    }

    [Fact]
    public void WindowsStyleSwitches_AreAccepted()
    {
        var result = CommandLineParser.Parse(["uninstall", "/silent", "/keep-data"]);

        Assert.True(result.IsValid);
        Assert.True(result.Options.Silent);
        Assert.True(result.Options.KeepData);
    }

    [Theory]
    [InlineData("--port", "0")]
    [InlineData("--port", "70000")]
    [InlineData("--port", "abc")]
    [InlineData("--keep-backups", "0")]
    [InlineData("--health-timeout", "1")]
    [InlineData("--health-url", "ftp://host/health")]
    [InlineData("--service-account", "admin")]
    public void InvalidValues_AreReported(string option, string value)
    {
        var result = CommandLineParser.Parse(["install", option, value]);

        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
    }

    [Fact]
    public void MissingValue_IsReported()
    {
        var result = CommandLineParser.Parse(["install", "--port"]);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("requires a value", StringComparison.Ordinal));
    }

    [Fact]
    public void UnknownCommandAndOption_AreReported()
    {
        var result = CommandLineParser.Parse(["deploy", "--turbo"]);

        Assert.Equal(2, result.Errors.Count);
    }

    [Theory]
    [InlineData(nameof(InstallerCommand.Auto), true)]
    [InlineData(nameof(InstallerCommand.Install), true)]
    [InlineData(nameof(InstallerCommand.Update), true)]
    [InlineData(nameof(InstallerCommand.Uninstall), true)]
    [InlineData(nameof(InstallerCommand.Status), false)]
    [InlineData(nameof(InstallerCommand.Help), false)]
    [InlineData(nameof(InstallerCommand.Version), false)]
    public void OnlyMachineChangingCommands_RequireElevation(string command, bool expected)
    {
        Assert.Equal(expected, CommandLineParser.RequiresElevation(Enum.Parse<InstallerCommand>(command)));
    }
}
