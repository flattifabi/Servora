using Servora.Application.Ai;
using Servora.Application.Common;

namespace Servora.Tests.Unit.Ai.Fakes;

/// <summary>
/// Fake <see cref="IAiChatClient"/> (a "fake provider"): answers each request with the result of a script function and
/// records every request for assertions.
/// </summary>
/// <param name="script">Produces the response for a request (the request index is passed as second argument).</param>
internal sealed class ScriptedChatClient(Func<AiChatRequest, int, Result<AiChatResponse>> script) : IAiChatClient
{
    /// <summary>Gets the received requests.</summary>
    public List<AiChatRequest> Requests { get; } = [];

    /// <summary>Creates a text response.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The successful result.</returns>
    public static Result<AiChatResponse> Text(string text) => Result<AiChatResponse>.Success(new AiChatResponse(text, [], "stop", 10, 5));

    /// <summary>Creates a response with one tool call.</summary>
    /// <param name="tool">The tool name.</param>
    /// <param name="argumentsJson">The arguments.</param>
    /// <returns>The successful result.</returns>
    public static Result<AiChatResponse> Call(string tool, string argumentsJson) =>
        Result<AiChatResponse>.Success(new AiChatResponse(string.Empty, [new AiToolCallRequest($"call_{Guid.NewGuid():N}", tool, argumentsJson)], "tool_calls", 20, 8));

    /// <inheritdoc />
    public Task<Result<AiChatResponse>> CompleteAsync(AiProviderConnection connection, AiChatRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(script(request, Requests.Count - 1));
    }
}
