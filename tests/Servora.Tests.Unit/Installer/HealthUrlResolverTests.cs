using Servora.Installer.Core;

namespace Servora.Tests.Unit.Installer;

/// <summary>Tests for <see cref="HealthUrlResolver"/>.</summary>
public sealed class HealthUrlResolverTests
{
    [Theory]
    [InlineData("http://0.0.0.0:5080", "http://localhost:5080/health")]
    [InlineData("http://*:8080", "http://localhost:8080/health")]
    [InlineData("http://+:8080", "http://localhost:8080/health")]
    [InlineData("http://[::]:9000", "http://localhost:9000/health")]
    [InlineData("http://servora.local", "http://servora.local:80/health")]
    [InlineData("https://0.0.0.0:5443", "https://localhost:5443/health")]
    [InlineData("https://servora.example.com", "https://servora.example.com:443/health")]
    public void Resolve_MapsWildcardsToLocalhost(string configured, string expected)
    {
        Assert.Equal(new Uri(expected), HealthUrlResolver.Resolve([configured]));
    }

    [Fact]
    public void Resolve_PrefersPlainHttpEndpoint()
    {
        Assert.Equal(new Uri("http://localhost:5080/health"), HealthUrlResolver.Resolve(["https://0.0.0.0:5443", "http://0.0.0.0:5080"]));
    }

    [Fact]
    public void Resolve_WithoutUsableUrl_FallsBackToDefaultPort()
    {
        Assert.Equal(new Uri("http://localhost:5080/health"), HealthUrlResolver.Resolve(["not a url"]));
        Assert.Equal(new Uri("http://localhost:5080/health"), HealthUrlResolver.Resolve([]));
    }

    [Fact]
    public void ResolveBase_AndFirstPort_UseTheSameUrl()
    {
        Assert.Equal(new Uri("http://localhost:6000/"), HealthUrlResolver.ResolveBase(["http://0.0.0.0:6000"]));
        Assert.Equal(6000, HealthUrlResolver.FirstPort(["http://0.0.0.0:6000", "http://0.0.0.0:7000"]));
        Assert.Null(HealthUrlResolver.FirstPort([]));
    }
}
