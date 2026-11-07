using System.Net;
using Servora.Application.Common;

namespace Servora.Application.Api;

/// <summary>Parses, normalizes and evaluates the allowed client IP ranges of an API key (single addresses or CIDR).</summary>
public static class ApiKeyIpRanges
{
    /// <summary>The maximum number of entries per key.</summary>
    public const int MaxEntries = 50;

    private static readonly char[] s_separators = [',', ';', ' ', '\n', '\r', '\t'];

    /// <summary>Validates and normalizes user input to a comma-separated list.</summary>
    /// <param name="input">The raw input (comma, semicolon, space or newline separated).</param>
    /// <returns>The normalized list (<see langword="null"/> for "any address") or a validation error.</returns>
    public static Result<string?> Normalize(string? input)
    {
        var entries = Split(input);
        if (entries.Count == 0)
        {
            return Result<string?>.Success(null);
        }

        if (entries.Count > MaxEntries)
        {
            return Result<string?>.Invalid($"At most {MaxEntries} IP ranges are allowed.");
        }

        var normalized = new List<string>();
        foreach (var entry in entries)
        {
            if (!TryParseEntry(entry, out var network))
            {
                return Result<string?>.Invalid($"'{Truncate(entry)}' is not a valid IP address or CIDR range (for example 10.0.0.0/8).");
            }

            normalized.Add(network.ToString());
        }

        return Result<string?>.Success(string.Join(", ", normalized.Distinct(StringComparer.OrdinalIgnoreCase)));
    }

    /// <summary>Parses a stored list into networks. Invalid entries are ignored (they cannot be stored).</summary>
    /// <param name="ranges">The stored list.</param>
    /// <returns>The networks; empty means any address.</returns>
    public static IReadOnlyList<IPNetwork> Parse(string? ranges)
    {
        var result = new List<IPNetwork>();
        foreach (var entry in Split(ranges))
        {
            if (TryParseEntry(entry, out var network))
            {
                result.Add(network);
            }
        }

        return result;
    }

    /// <summary>Checks whether a client address is allowed.</summary>
    /// <param name="networks">The allowed networks; empty allows every address.</param>
    /// <param name="address">The client address.</param>
    /// <returns><see langword="true"/> when allowed.</returns>
    public static bool IsAllowed(IReadOnlyList<IPNetwork> networks, IPAddress? address)
    {
        if (networks.Count == 0)
        {
            return true;
        }

        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        foreach (var network in networks)
        {
            if (network.BaseAddress.AddressFamily == address.AddressFamily && network.Contains(address))
            {
                return true;
            }
        }

        return false;
    }

    private static List<string> Split(string? input) =>
        string.IsNullOrWhiteSpace(input) ? [] : input.Split(s_separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

    private static bool TryParseEntry(string entry, out IPNetwork network)
    {
        if (entry.Contains('/'))
        {
            return IPNetwork.TryParse(entry, out network);
        }

        if (IPAddress.TryParse(entry, out var address))
        {
            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            network = new IPNetwork(address, address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128);
            return true;
        }

        network = default;
        return false;
    }

    private static string Truncate(string value) => value.Length <= 60 ? value : value[..60] + "…";
}
