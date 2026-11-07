using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="LayoutResolver"/>.</summary>
public sealed class LayoutResolverTests
{
    [Theory]
    [InlineData("\"C:\\Program Files\\Servora\\Servora.exe\"", @"C:\Program Files\Servora\Servora.exe")]
    [InlineData("\"C:\\Program Files\\Servora\\Servora.exe\" --contentRoot x", @"C:\Program Files\Servora\Servora.exe")]
    [InlineData(@"D:\Servora\Servora.exe", @"D:\Servora\Servora.exe")]
    [InlineData(@"D:\Servora\Servora.exe --urls http://+:80", @"D:\Servora\Servora.exe")]
    public void ExecutablePathFromImagePath_HandlesQuotesAndArguments(string imagePath, string expected)
    {
        Assert.Equal(expected, LayoutResolver.ExecutablePathFromImagePath(imagePath));
    }

    [Fact]
    public void ResolveDataDirectory_UsesServiceThenMachineVariableThenProgramData()
    {
        var services = new FakeServiceManager();
        var environment = new FakeInstallerEnvironment();
        var resolver = new LayoutResolver(services, environment);

        Assert.Equal(@"C:\ProgramData\Servora", resolver.ResolveDataDirectory());

        environment.MachineVariables["SERVORA_DATA_DIR"] = @"E:\MachineData\";
        Assert.Equal(@"E:\MachineData", resolver.ResolveDataDirectory());

        services.Environment["SERVORA_DATA_DIR"] = @"D:\ServiceData";
        Assert.Equal(@"D:\ServiceData", resolver.ResolveDataDirectory());
    }

    [Theory]
    [InlineData(@"C:\Program Files\Servora", true)]
    [InlineData(@"D:\Apps\Servora", true)]
    [InlineData(@"C:\", false)]
    [InlineData(@"Servora", false)]
    [InlineData(@"C:\Windows\System32\Servora", false)]
    [InlineData(@"C:\ProgramData\Servora", false)]
    [InlineData(@"C:\ProgramData", false)]
    public void ValidateInstallDirectory_RejectsDangerousLocations(string candidate, bool valid)
    {
        var resolver = new LayoutResolver(new FakeServiceManager(), new FakeInstallerEnvironment());

        var error = resolver.ValidateInstallDirectory(candidate, @"C:\ProgramData\Servora");

        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void PreservedEntries_IncludeAppDataAndNestedDataDirectory()
    {
        var preserved = LayoutResolver.PreservedEntries(@"D:\Servora", @"D:\Servora\data\instance");

        Assert.Contains(ProductInfo.LegacyDataFolderName, preserved);
        Assert.Contains("data", preserved);
        Assert.Contains("DATA", preserved);
        Assert.Equal(2, preserved.Count);
    }

    [Fact]
    public void ForInstalledService_DerivesInstallDirectoryFromImagePath()
    {
        var services = new FakeServiceManager();
        services.Install(@"D:\Apps\Servora\Servora.exe");
        var resolver = new LayoutResolver(services, new FakeInstallerEnvironment());

        var layout = resolver.ForInstalledService(services.Service!);

        Assert.Equal(@"D:\Apps\Servora", layout.InstallDirectory);
        Assert.Equal(@"C:\ProgramData\Servora", layout.DataDirectory);
        Assert.Equal(@"C:\ProgramData\Servora\backups", layout.BackupDirectory);
        Assert.Equal(@"C:\ProgramData\Servora\appsettings.json", layout.InstanceSettingsPath);
    }
}
