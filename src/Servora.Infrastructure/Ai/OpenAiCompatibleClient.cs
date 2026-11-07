using System.Net.Http.Headers;
using Servora.Application.Ai;
using Servora.Application.Common;
using Servora.Domain.Ai;

namespace Servora.Infrastructure.Ai;

/// <summary>Client of OpenAI-compatible Chat Completions endpoints (OpenAI, Ollama, LM Studio, vLLM, ...).</summary>
/// <param name="transport">The HTTP transport.</param>
public sealed class OpenAiCompatibleClient(AiHttpTransport transport) : IAiProviderClient
{
    /// <inheritdoc />
    public AiProviderKind Kind => AiProviderKind.OpenAiCompatible;

    /// <inheritdoc />
    public async Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate($"{connection.BaseUrl.TrimEnd('/')}/chat/completions", UriKind.Absolute, out var url))
        {
            return Result<AiChatResponse>.Invalid("The provider base URL is invalid.");
        }

        var isOpenAi = url.Host.Equals("api.openai.com", StringComparison.OrdinalIgnoreCase);
        var body = OpenAiWireFormat.BuildBody(connection, request, includeModel: true, useMaxCompletionTokens: isOpenAi);
        var response = await transport.PostJsonAsync(
            connection,
            url,
            body,
            headers =>
            {
                if (!string.IsNullOrWhiteSpace(connection.ApiKey))
                {
                    headers.Authorization = new AuthenticationHeaderValue("Bearer", connection.ApiKey);
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
