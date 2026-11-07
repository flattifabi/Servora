using System.Security.Authentication;
using Servora.Installer.Abstractions;
using Servora.Installer.Model;

namespace Servora.Installer.Platform;

/// <summary>
/// <see cref="IHealthProbe"/> using <see cref="HttpClient"/>. Certificate validation stays enabled; a local probe
/// bypasses any configured proxy because the target is the local machine.
/// </summary>
internal sealed class HttpHealthProbe : IHealthProbe, IDisposable
{
    private readonly HttpClient _client = new(new SocketsHttpHandler { UseProxy = false, AllowAutoRedirect = false })
    {
        Timeout = TimeSpan.FromSeconds(5),
    };

    /// <inheritdoc />
    public async Task<HealthProbeResult> ProbeAsync(Uri healthUrl, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _client.GetAsync(healthUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode ? HealthProbeResult.Healthy : HealthProbeResult.Unhealthy;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or AuthenticationException && !cancellationToken.IsCancellationRequested)
        {
            return HealthProbeResult.Unreachable;
        }
    }

    /// <inheritdoc />
    public void Dispose() => _client.Dispose();
}
