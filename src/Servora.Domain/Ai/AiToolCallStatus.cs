namespace Servora.Domain.Ai;

/// <summary>Status of a step in the AI execution trace.</summary>
public enum AiToolCallStatus
{
    /// <summary>The step completed successfully.</summary>
    Succeeded = 0,

    /// <summary>The step failed (tool error, invalid arguments).</summary>
    Failed = 1,

    /// <summary>The step was denied because the agent identity lacks the permission.</summary>
    Denied = 2,

    /// <summary>A consequential action waits for explicit user confirmation.</summary>
    PendingConfirmation = 3,

    /// <summary>The user confirmed the action and it was executed.</summary>
    Confirmed = 4,

    /// <summary>The user rejected the action.</summary>
    Rejected = 5,
}
