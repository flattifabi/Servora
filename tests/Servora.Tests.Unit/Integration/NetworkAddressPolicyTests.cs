using System.Net;
using Servora.Infrastructure.Integration;

namespace Servora.Tests.Unit.Integration;

/// <summary>Tests of the SSRF address classification <see cref="NetworkAddressPolicy"/>.</summary>
public sealed class NetworkAddressPolicyTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("127.10.20.30")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.1.10")]
    [InlineData("169.254.10.10")]
    [InlineData("169.254.169.254")]
    [InlineData("169.254.170.2")]
    [InlineData("100.100.100.200")]
    [InlineData("100.64.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("fd12:3456::1")]
    [InlineData("fd00:ec2::254")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("::ffff:169.254.169.254")]
    [InlineData("64:ff9b::a9fe:a9fe")]
    [InlineData("2002:7f00:0001::1")]
    [InlineData("::127.0.0.1")]
    public void BlocksInternalAndMetadataAddresses(string address)
    {
        Assert.NotNull(NetworkAddressPolicy.GetBlockReason(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("140.82.112.3")]
    [InlineData("1.1.1.1")]
    [InlineData("8.8.8.8")]
    [InlineData("172.32.0.1")]
    [InlineData("2606:4700:4700::1111")]
    [InlineData("::ffff:1.1.1.1")]
    public void AllowsPublicAddresses(string address)
    {
        Assert.Null(NetworkAddressPolicy.GetBlockReason(IPAddress.Parse(address)));
    }

    [Fact]
    public void AllowList_OpensRestrictedRanges()
    {
        var allow = new[] { IPNetwork.Parse("10.20.0.0/16") };

        Assert.Null(NetworkAddressPolicy.GetBlockReason(IPAddress.Parse("10.20.5.5"), allow));
        Assert.NotNull(NetworkAddressPolicy.GetBlockReason(IPAddress.Parse("10.21.5.5"), allow));
    }

    [Fact]
    public void AllowList_NeverOpensMetadataEndpoints()
    {
        var allow = new[] { IPNetwork.Parse("169.254.0.0/16"), IPNetwork.Parse("0.0.0.0/0") };

        Assert.Equal("cloud metadata endpoint", NetworkAddressPolicy.GetBlockReason(IPAddress.Parse("169.254.169.254"), allow));
    }

    [Fact]
    public void Normalize_ExtractsEmbeddedIPv4()
    {
        Assert.Equal(IPAddress.Parse("127.0.0.1"), NetworkAddressPolicy.Normalize(IPAddress.Parse("::ffff:127.0.0.1")));
        Assert.Equal(IPAddress.Parse("169.254.169.254"), NetworkAddressPolicy.Normalize(IPAddress.Parse("64:ff9b::a9fe:a9fe")));
        Assert.Equal(IPAddress.Parse("::1"), NetworkAddressPolicy.Normalize(IPAddress.Parse("::1")));
    }
}
