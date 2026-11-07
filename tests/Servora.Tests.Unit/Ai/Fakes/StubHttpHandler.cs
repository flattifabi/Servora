using System.Net;
using System.Text;

namespace Servora.Tests.Unit.Ai.Fakes;

/// <summary>HTTP handler that returns a fixed response and captures the request (method, URL, headers, body).</summary>
/// <param name="status">The status code to return.</param>
/// <param name="responseJson">The response body.</param>
internal sealed class StubHttpHandler(HttpStatusCode status, string responseJson) : HttpMessageHandler
{
    /// <summary>Gets the captured request.</summary>
    public HttpRequestMessage? Request { get; private set; }

    /// <summary>Gets the captured request body.</summary>
    public string? RequestBody { get; private set; }

    /// <inheritdoc />
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        return new HttpResponseMessage(status) { Content = new StringContent(responseJson, Encoding.UTF8, "application/json") };
    }
}
