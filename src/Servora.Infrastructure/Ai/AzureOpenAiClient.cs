using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Ai;

/// <summary>Client of Azure OpenAI deployments (<c>/openai/deployments/{deployment}/chat/completions</c>, <c>api-key</c> header).</summary>
/// <param name="transport">The HTTP transport.</param>
public sealed class AzureOpenAiClient(AiHttpTransport transport) : IAiProviderClient
{
    /// <inheritdoc />
    public AiProviderKind Kind => AiProviderKind.AzureOpenAi;

    /// <inheritdoc />
    public async Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.Deployment))
        {
            return Result<AiChatResponse>.Invalid("The Azure OpenAI deployment is not configured.");
        }

        var apiVersion = string.IsNullOrWhiteSpace(connection.ApiVersion) ? "2024-10-21" : connection.ApiVersion;
        var address = $"{connection.BaseUrl.TrimEnd('/')}/openai/deployments/{Uri.EscapeDataString(connection.Deployment)}/chat/completions?api-version={Uri.EscapeDataString(apiVersion)}";
        if (!Uri.TryCreate(address, UriKind.Absolute, out var url))
        {
            return Result<AiChatResponse>.Invalid("The provider base URL is invalid.");
        }

        var body = OpenAiWireFormat.BuildBody(connection, request, includeModel: false, useMaxCompletionTokens: false);
        var response = await transport.PostJsonAsync(
            connection,
            url,
            body,
            headers =>
            {
                if (!string.IsNullOrWhiteSpace(connection.ApiKey))
                {
                    headers.Add("api-key", connection.ApiKey);
                }
            },
            cancellationToken);
        if (!response.Succeeded)
        {
            return Result<AiChatResponse>.From(response);
        }

        return OpenAiWireFormat.Parse(response.Value!) is { } parsed
            ? Result<AiChatResponse>.Success(parsed)
            : Result<AiChatResponse>.Failure("The provider response has an unexpected format.");
    }
}
