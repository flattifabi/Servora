namespace Servora.Application.Ai;

/// <summary>Mutable collector of trace steps and token usage during one orchestration run.</summary>
internal sealed class AiRunAccumulator
{
    /// <summary>Gets the collected steps.</summary>
    public List<AiTraceStep> Steps { get; } = [];

    /// <summary>Gets or sets the total input tokens.</summary>
    public int InputTokens { get; set; }

    /// <summary>Gets or sets the total output tokens.</summary>
    public int OutputTokens { get; set; }

    /// <summary>Adds the usage of a response.</summary>
    /// <param name="response">The response.</param>
    public void AddUsage(AiChatResponse response)
    {
        InputTokens += response.InputTokens;
        OutputTokens += response.OutputTokens;
    }
}
