namespace Servora.Tests.Unit.Ai.Fakes;

/// <summary><see cref="IHttpClientFactory"/> returning clients bound to one handler.</summary>
/// <param name="handler">The handler.</param>
internal sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
{
    /// <inheritdoc />
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
