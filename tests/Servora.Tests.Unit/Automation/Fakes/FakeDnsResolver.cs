using System.Net;
using Servora.Infrastructure.Integration;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary>DNS resolver with a fixed host table (unknown hosts do not resolve).</summary>
internal sealed class FakeDnsResolver : IDnsResolver
{
    /// <summary>Gets the host table.</summary>
    public Dictionary<string, IPAddress[]> Hosts { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <inheritdoc />
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Task.FromResult(Hosts.TryGetValue(host, out var addresses) ? addresses : []);
}
