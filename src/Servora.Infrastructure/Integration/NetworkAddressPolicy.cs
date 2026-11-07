using System.Net;
using System.Net.Sockets;

namespace Servora.Infrastructure.Integration;

/// <summary>
/// Classifies IP addresses for SSRF protection. Cloud metadata addresses, unspecified, multicast and reserved ranges are
/// always blocked; loopback, private, link-local, unique-local, shared (CGNAT) and documentation ranges are blocked
/// unless an administrator allow-list range contains the address. IPv4-mapped / compatible / NAT64 / 6to4 IPv6
/// addresses are checked by their embedded IPv4 address.
/// </summary>
public static class NetworkAddressPolicy
{
    private static readonly IPAddress[] s_metadata =
    [
        IPAddress.Parse("169.254.169.254"),
        IPAddress.Parse("169.254.169.253"),
        IPAddress.Parse("169.254.170.2"),
        IPAddress.Parse("100.100.100.200"),
        IPAddress.Parse("fd00:ec2::254"),
    ];

    private static readonly (IPNetwork Network, string Reason)[] s_alwaysBlocked =
    [
        (IPNetwork.Parse("0.0.0.0/8"), "unspecified"),
        (IPNetwork.Parse("224.0.0.0/4"), "multicast"),
        (IPNetwork.Parse("240.0.0.0/4"), "reserved"),
        (IPNetwork.Parse("::/128"), "unspecified"),
        (IPNetwork.Parse("ff00::/8"), "multicast"),
    ];

    private static readonly (IPNetwork Network, string Reason)[] s_restricted =
    [
        (IPNetwork.Parse("127.0.0.0/8"), "loopback"),
        (IPNetwork.Parse("10.0.0.0/8"), "private network"),
        (IPNetwork.Parse("172.16.0.0/12"), "private network"),
        (IPNetwork.Parse("192.168.0.0/16"), "private network"),
        (IPNetwork.Parse("169.254.0.0/16"), "link-local"),
        (IPNetwork.Parse("100.64.0.0/10"), "shared address space"),
        (IPNetwork.Parse("192.0.0.0/24"), "IETF protocol assignments"),
        (IPNetwork.Parse("192.0.2.0/24"), "documentation"),
        (IPNetwork.Parse("198.51.100.0/24"), "documentation"),
        (IPNetwork.Parse("203.0.113.0/24"), "documentation"),
        (IPNetwork.Parse("198.18.0.0/15"), "benchmarking"),
        (IPNetwork.Parse("192.88.99.0/24"), "6to4 relay"),
        (IPNetwork.Parse("::1/128"), "loopback"),
        (IPNetwork.Parse("fe80::/10"), "link-local"),
        (IPNetwork.Parse("fec0::/10"), "site-local"),
        (IPNetwork.Parse("fc00::/7"), "unique local address"),
        (IPNetwork.Parse("2001:db8::/32"), "documentation"),
        (IPNetwork.Parse("2001::/32"), "Teredo tunnel"),
        (IPNetwork.Parse("100::/64"), "discard prefix"),
    ];

    private static readonly IPNetwork s_nat64 = IPNetwork.Parse("64:ff9b::/96");
    private static readonly IPNetwork s_sixToFour = IPNetwork.Parse("2002::/16");

    /// <summary>Gets the reason an address is blocked, or <see langword="null"/> when it may be contacted.</summary>
    /// <param name="address">The address.</param>
    /// <param name="allowList">Administrator-approved ranges.</param>
    /// <returns>The reason or <see langword="null"/>.</returns>
    public static string? GetBlockReason(IPAddress address, IReadOnlyList<IPNetwork>? allowList = null)
    {
        var ip = Normalize(address);
        if (s_metadata.Any(m => m.Equals(ip)))
        {
            return "cloud metadata endpoint";
        }

        foreach (var (network, reason) in s_alwaysBlocked)
        {
            if (Contains(network, ip))
            {
                return reason;
            }
        }

        if (ip.Equals(IPAddress.Broadcast))
        {
            return "broadcast";
        }

        string? restricted = null;
        foreach (var (network, reason) in s_restricted)
        {
            if (Contains(network, ip))
            {
                restricted = reason;
                break;
            }
        }

        if (restricted is null)
        {
            return null;
        }

        return allowList is not null && allowList.Any(n => Contains(n, ip)) ? null : restricted;
    }

    /// <summary>Normalizes IPv6 forms that embed an IPv4 address to that IPv4 address.</summary>
    /// <param name="address">The address.</param>
    /// <returns>The normalized address.</returns>
    public static IPAddress Normalize(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            return address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (s_nat64.Contains(address))
        {
            return new IPAddress(bytes.AsSpan(12, 4));
        }

        if (s_sixToFour.Contains(address))
        {
            return new IPAddress(bytes.AsSpan(2, 4));
        }

        // Deprecated IPv4-compatible addresses (::a.b.c.d), excluding :: and ::1.
        if (bytes.AsSpan(0, 12).IndexOfAnyExcept((byte)0) < 0 && !(bytes[12] == 0 && bytes[13] == 0 && bytes[14] == 0 && bytes[15] <= 1))
        {
            return new IPAddress(bytes.AsSpan(12, 4));
        }

        return address;
    }

    private static bool Contains(IPNetwork network, IPAddress address) =>
        network.BaseAddress.AddressFamily == address.AddressFamily && network.Contains(address);
}
