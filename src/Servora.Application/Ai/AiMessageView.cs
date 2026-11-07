using Servora.Domain.Ai;

namespace Servora.Application.Ai;

/// <summary>A conversation message with its trace steps.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Role">The role.</param>
/// <param name="Content">The text.</param>
/// <param name="AgentKey">The primary agent key.</param>
/// <param name="AgentName">The primary agent name.</param>
/// <param name="IsError">Whether the message reports an error.</param>
/// <param name="CreatedAt">The UTC time.</param>
/// <param name="DurationMs">The end-to-end duration.</param>
/// <param name="Steps">The trace steps.</param>
public sealed record AiMessageView(
    Guid Id,
    AiMessageRole Role,
    string Content,
    string? AgentKey,
    string? AgentName,
    bool IsError,
    DateTimeOffset CreatedAt,
    long? DurationMs,
    IReadOnlyList<AiToolCallView> Steps);
