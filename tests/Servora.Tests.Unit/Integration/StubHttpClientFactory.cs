namespace Servora.Tests.Unit.Integration;

/// <summary><see cref="IHttpClientFactory"/> returning clients over a fixed handler.</summary>
/// <param name="handler">The handler.</param>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false) { Timeout = Timeout.InfiniteTimeSpan };
}
