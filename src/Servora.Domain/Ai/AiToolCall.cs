using Servora.Domain.Common;

namespace Servora.Domain.Ai;

/// <summary>
/// One step of the persisted AI execution trace: a delegation from the orchestrator to a sub-agent or a tool call
/// made by a sub-agent (including consequential actions awaiting confirmation).
/// </summary>
public class AiToolCall : Entity
{
    /// <summary>Gets or sets the conversation identifier.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Gets or sets the assistant message the step belongs to.</summary>
    public Guid MessageId { get; set; }

    /// <summary>Gets or sets the order of the step within the message.</summary>
    public int Sequence { get; set; }

    /// <summary>Gets or sets the step kind: <c>delegation</c>, <c>tool</c> or <c>action</c>.</summary>
    public string Kind { get; set; } = "tool";

    /// <summary>Gets or sets the key of the agent that performed the step (<c>orchestrator</c> for delegations).</summary>
    public string AgentKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the tool name, or the target agent key for delegations.</summary>
    public string ToolName { get; set; } = string.Empty;

    /// <summary>Gets or sets the JSON arguments.</summary>
    public string? ArgumentsJson { get; set; }

    /// <summary>Gets or sets the bounded result (JSON or text).</summary>
    public string? ResultJson { get; set; }

    /// <summary>Gets or sets the status.</summary>
    public AiToolCallStatus Status { get; set; }

    /// <summary>Gets or sets a user-safe error message.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Gets or sets the display name of the scoped agent identity that ran the step.</summary>
    public string? ActorName { get; set; }

    /// <summary>Gets or sets the duration in milliseconds.</summary>
    public long DurationMs { get; set; }

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the UTC time the user confirmed or rejected the action.</summary>
    public DateTimeOffset? DecidedAt { get; set; }
}
