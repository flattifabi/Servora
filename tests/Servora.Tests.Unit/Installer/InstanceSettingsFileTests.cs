using System.Text.Json;
using Servora.Installer.Core;
using Servora.Installer.Model;
using Servora.Tests.Unit.Installer.Fakes;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="InstanceSettingsFile"/>.</summary>
public sealed class InstanceSettingsFileTests
{
    private const string SettingsPath = @"C:\ProgramData\Servora\appsettings.json";

    [Fact]
    public void SetUrls_CreatesValidJsonWhenMissing()
    {
        var fs = new InMemoryFileSystem();

        new InstanceSettingsFile(fs).SetUrls(SettingsPath, InstanceSettingsFile.UrlsForPort(8080));

        using var document = JsonDocument.Parse(fs.Content(SettingsPath)!);
        Assert.Equal("http://0.0.0.0:8080", document.RootElement.GetProperty("Urls").GetString());
    }

    [Fact]
    public void SetUrls_PreservesOtherKeys_AndReplacesKeyCaseInsensitively()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile(SettingsPath, """
            {
              // operator comment
              "URLS": "http://0.0.0.0:5080",
              "Kestrel": { "Certificates": { "Default": { "Path": "C:\\certs\\servora.pfx" } } },
            }
            """);

        new InstanceSettingsFile(fs).SetUrls(SettingsPath, "http://0.0.0.0:9000");

        using var document = JsonDocument.Parse(fs.Content(SettingsPath)!);
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal(["Kestrel", "Urls"], names.Order(StringComparer.Ordinal));
        Assert.Equal("http://0.0.0.0:9000", document.RootElement.GetProperty("Urls").GetString());
        Assert.Equal(@"C:\certs\servora.pfx", document.RootElement.GetProperty("Kestrel").GetProperty("Certificates").GetProperty("Default").GetProperty("Path").GetString());
    }

    [Fact]
    public void SetUrls_InvalidJson_Throws()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile(SettingsPath, "{ not json");

        Assert.Throws<InstallerException>(() => new InstanceSettingsFile(fs).SetUrls(SettingsPath, "http://0.0.0.0:9000"));
        Assert.Equal("{ not json", fs.Content(SettingsPath));
    }

    [Fact]
    public void ReadUrls_CombinesUrlsAndKestrelEndpoints()
    {
        var fs = new InMemoryFileSystem();
        fs.AddFile(SettingsPath, """
            {
              "Urls": "http://0.0.0.0:5080; http://0.0.0.0:5081",
              "Kestrel": { "Endpoints": { "Https": { "Url": "https://0.0.0.0:5443" } } }
            }
            """);

        var urls = new InstanceSettingsFile(fs).ReadUrls(SettingsPath);

        Assert.Equal(["http://0.0.0.0:5080", "http://0.0.0.0:5081", "https://0.0.0.0:5443"], urls);
    }

    [Fact]
    public void ReadUrls_MissingOrInvalidFile_ReturnsEmpty()
    {
        var fs = new InMemoryFileSystem();
        Assert.Empty(new InstanceSettingsFile(fs).ReadUrls(SettingsPath));

        fs.AddFile(SettingsPath, "[1, 2]");
        Assert.Empty(new InstanceSettingsFile(fs).ReadUrls(SettingsPath));
    }
}
