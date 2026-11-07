using System.Net;
using Servora.Application.Api;

namespace Servora.Tests.Unit.Api;

/// <summary>Tests for <see cref="ApiKeyIpRanges"/>.</summary>
public sealed class ApiKeyIpRangesTests
{
    [Fact]
    public void Normalize_AcceptsAddressesAndCidrRanges()
    {
        var result = ApiKeyIpRanges.Normalize("10.0.0.0/8, 192.168.1.10; ::1");

        Assert.True(result.Succeeded, result.Error);
        Assert.Contains("10.0.0.0/8", result.Value);
        Assert.Contains("192.168.1.10/32", result.Value);
    }

    [Fact]
    public void Normalize_RejectsGarbageAndEmptyMeansUnrestricted()
    {
        Assert.False(ApiKeyIpRanges.Normalize("10.0.0.0/8, not-an-ip").Succeeded);
        Assert.False(ApiKeyIpRanges.Normalize("10.0.0.0/99").Succeeded);
        Assert.Null(ApiKeyIpRanges.Normalize("   ").Value);
    }

    [Fact]
    public void IsAllowed_MatchesConfiguredNetworksOnly()
    {
        var networks = ApiKeyIpRanges.Parse("10.0.0.0/8, 192.168.1.10");

        Assert.True(ApiKeyIpRanges.IsAllowed(networks, IPAddress.Parse("10.20.30.40")));
        Assert.True(ApiKeyIpRanges.IsAllowed(networks, IPAddress.Parse("192.168.1.10")));
        Assert.True(ApiKeyIpRanges.IsAllowed(networks, IPAddress.Parse("::ffff:10.1.2.3")));
        Assert.False(ApiKeyIpRanges.IsAllowed(networks, IPAddress.Parse("192.168.1.11")));
        Assert.False(ApiKeyIpRanges.IsAllowed(networks, null));
        Assert.True(ApiKeyIpRanges.IsAllowed([], IPAddress.Parse("8.8.8.8")));
    }
}
