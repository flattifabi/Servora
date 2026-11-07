using System.Net;
using Servora.Application.Integration.DataSources;

namespace Servora.Tests.Unit.Automation.Fakes;

/// <summary><see cref="INetworkAllowListProvider"/> with a fixed list of ranges.</summary>
internal sealed class StaticAllowListProvider : INetworkAllowListProvider
{
    /// <summary>Gets the allowed ranges.</summary>
    public List<IPNetwork> Networks { get; } = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<IPNetwork>> GetAllowedNetworksAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<IPNetwork>>(Networks);

    /// <inheritdoc />
    public void Invalidate()
    {
    }
}
