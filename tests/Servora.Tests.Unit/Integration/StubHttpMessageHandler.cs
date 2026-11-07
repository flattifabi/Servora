using System.Collections.Concurrent;

namespace Servora.Tests.Unit.Integration;

/// <summary>HTTP handler that answers requests with a test-supplied function and records them.</summary>
/// <param name="respond">Creates the response for a request.</param>
internal sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    /// <summary>Gets the received requests (URI and headers).</summary>
    public ConcurrentQueue<HttpRequestMessage> Requests { get; } = new();

    /// <inheritdoc />
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Enqueue(request);
        return Task.FromResult(respond(request));
    }
}
