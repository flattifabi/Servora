using Servora.Domain.Common;

namespace Servora.Domain.Ai;

/// <summary>One message of an <see cref="AiConversation"/>.</summary>
public class AiMessage : Entity
{
    /// <summary>Gets or sets the conversation identifier.</summary>
    public Guid ConversationId { get; set; }

    /// <summary>Gets or sets the author role.</summary>
    public AiMessageRole Role { get; set; }

    /// <summary>Gets or sets the text content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Gets or sets the key of the sub-agent that produced the answer, if any.</summary>
    public string? AgentKey { get; set; }

    /// <summary>Gets or sets a value indicating whether the message reports an error.</summary>
    public bool IsError { get; set; }

    /// <summary>Gets or sets the UTC creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the number of input tokens reported by the provider.</summary>
    public int? InputTokens { get; set; }

    /// <summary>Gets or sets the number of output tokens reported by the provider.</summary>
    public int? OutputTokens { get; set; }

    /// <summary>Gets or sets the end-to-end duration in milliseconds.</summary>
    public long? DurationMs { get; set; }
}
