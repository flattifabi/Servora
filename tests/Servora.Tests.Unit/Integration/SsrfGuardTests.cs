using System.Net;
using Servora.Infrastructure.Integration;
using Servora.Tests.Unit.Automation.Fakes;

namespace Servora.Tests.Unit.Integration;

/// <summary>Tests of the URL / DNS validation of <see cref="SsrfGuard"/>.</summary>
public sealed class SsrfGuardTests
{
    private readonly FakeDnsResolver _dns = new();
    private readonly StaticAllowListProvider _allow = new();

    private SsrfGuard Guard => new(_dns, _allow);

    [Theory]
    [InlineData("http://127.0.0.1/admin", true)]
    [InlineData("https://127.0.0.1/admin", false)]
    [InlineData("https://169.254.169.254/latest/meta-data/", false)]
    [InlineData("http://169.254.169.254/latest/meta-data/", true)]
    [InlineData("https://[::1]/", false)]
    [InlineData("https://[::ffff:10.0.0.1]/", false)]
    [InlineData("https://0x7f000001/", false)]
    [InlineData("https://2130706433/", false)]
    public async Task BlocksLoopbackPrivateAndMetadataLiterals(string url, bool allowHttp)
    {
        var error = await Guard.ValidateUriAsync(new Uri(url), allowHttp, CancellationToken.None);

        Assert.NotNull(error);
    }

    [Fact]
    public async Task BlocksPlainHttpByDefault()
    {
        _dns.Hosts["api.example.com"] = [IPAddress.Parse("93.184.216.34")];

        Assert.Contains("HTTPS", await Guard.ValidateUriAsync(new Uri("http://api.example.com/"), false, CancellationToken.None));
        Assert.Null(await Guard.ValidateUriAsync(new Uri("http://api.example.com/"), true, CancellationToken.None));
    }

    [Theory]
    [InlineData("ftp://api.example.com/file")]
    [InlineData("file:///c:/windows/win.ini")]
    [InlineData("gopher://api.example.com/")]
    public async Task BlocksOtherSchemes(string url)
    {
        Assert.NotNull(await Guard.ValidateUriAsync(new Uri(url), true, CancellationToken.None));
    }

    [Fact]
    public async Task BlocksEmbeddedCredentials()
    {
        _dns.Hosts["api.example.com"] = [IPAddress.Parse("93.184.216.34")];

        Assert.Contains("credentials", await Guard.ValidateUriAsync(new Uri("https://user:pass@api.example.com/"), false, CancellationToken.None));
    }

    [Fact]
    public async Task BlocksHostNamesResolvingToInternalAddresses()
    {
        _dns.Hosts["internal.example.com"] = [IPAddress.Parse("10.0.0.5")];
        _dns.Hosts["rebind.example.com"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("169.254.169.254")];
        _dns.Hosts["localhost.example.com"] = [IPAddress.Parse("127.0.0.1")];

        Assert.Contains("private network", await Guard.ValidateUriAsync(new Uri("https://internal.example.com/"), false, CancellationToken.None));
        Assert.Contains("metadata", await Guard.ValidateUriAsync(new Uri("https://rebind.example.com/"), false, CancellationToken.None));
        Assert.Contains("loopback", await Guard.ValidateUriAsync(new Uri("https://localhost.example.com/"), false, CancellationToken.None));
    }

    [Fact]
    public async Task UnresolvableHostIsRejected()
    {
        Assert.Contains("could not be resolved", await Guard.ValidateUriAsync(new Uri("https://nowhere.example.com/"), false, CancellationToken.None));
    }

    [Fact]
    public async Task AllowsPublicHostsAndAllowListedRanges()
    {
        _dns.Hosts["api.github.com"] = [IPAddress.Parse("140.82.112.6")];
        _dns.Hosts["cmdb.corp.local"] = [IPAddress.Parse("10.20.1.5")];

        Assert.Null(await Guard.ValidateUriAsync(new Uri("https://api.github.com/zen"), false, CancellationToken.None));
        Assert.NotNull(await Guard.ValidateUriAsync(new Uri("https://cmdb.corp.local/api"), false, CancellationToken.None));

        _allow.Networks.Add(IPNetwork.Parse("10.20.0.0/16"));
        Assert.Null(await Guard.ValidateUriAsync(new Uri("https://cmdb.corp.local/api"), false, CancellationToken.None));
    }

    [Fact]
    public async Task ResolveAllowed_ReturnsOnlyValidatedAddresses()
    {
        _dns.Hosts["api.example.com"] = [IPAddress.Parse("93.184.216.34"), IPAddress.Parse("2606:2800:220:1::1")];

        var addresses = await Guard.ResolveAllowedAsync("api.example.com", CancellationToken.None);

        Assert.Equal(2, addresses.Count);
        await Assert.ThrowsAsync<DataSourceBlockedException>(() => Guard.ResolveAllowedAsync("[::1]", CancellationToken.None));
    }
}
