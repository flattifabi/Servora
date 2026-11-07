using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Ai;

/// <summary>Client of the Anthropic Messages API (<c>/v1/messages</c>, <c>x-api-key</c> and <c>anthropic-version</c> headers).</summary>
/// <param name="transport">The HTTP transport.</param>
public sealed class AnthropicClient(AiHttpTransport transport) : IAiProviderClient
{
    /// <inheritdoc />
    public AiProviderKind Kind => AiProviderKind.Anthropic;

    /// <inheritdoc />
    public async Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken)
    {
        var baseUrl = string.IsNullOrWhiteSpace(connection.BaseUrl) ? "https://api.anthropic.com" : connection.BaseUrl.TrimEnd('/');
        var address = baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase) ? $"{baseUrl}/messages" : $"{baseUrl}/v1/messages";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var url))
        {
            return Result<AiChatResponse>.Invalid("The provider base URL is invalid.");
        }

        if (string.IsNullOrWhiteSpace(connection.ApiKey))
        {
            return Result<AiChatResponse>.Invalid("The Anthropic API key is not configured.");
        }

        var response = await transport.PostJsonAsync(
            connection,
            url,
            AnthropicWireFormat.BuildBody(connection, request),
            headers =>
            {
                headers.Add("x-api-key", connection.ApiKey);
                headers.Add("anthropic-version", AnthropicWireFormat.ApiVersion);
            },
            cancellationToken);
        if (!response.Succeeded)
        {
            return Result<AiChatResponse>.From(response);
        }

        return AnthropicWireFormat.Parse(response.Value!) is { } parsed
            ? Result<AiChatResponse>.Success(parsed)
            : Result<AiChatResponse>.Failure("The provider response has an unexpected format.");
    }
}
