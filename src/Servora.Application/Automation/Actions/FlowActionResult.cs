using System.Text.Json.Nodes;

namespace Servora.Application.Automation.Actions;

/// <summary>Result of <see cref="IFlowAction.ExecuteAsync"/>.</summary>
public sealed class FlowActionResult
{
    private FlowActionResult(FlowActionOutcome outcome)
    {
        Outcome = outcome;
    }

    /// <summary>Gets the outcome.</summary>
    public FlowActionOutcome Outcome { get; }

    /// <summary>Gets the output (stored redacted under <c>steps.&lt;key&gt;</c>).</summary>
    public JsonNode? Output { get; private init; }

    /// <summary>Gets the branch to execute next (for branching steps).</summary>
    public string? Branch { get; private init; }

    /// <summary>Gets the user-safe error message.</summary>
    public string? Error { get; private init; }

    /// <summary>Gets a value indicating whether a failure is transient and may be retried.</summary>
    public bool Retryable { get; private init; }

    /// <summary>Gets the UTC time a waiting step should be checked again.</summary>
    public DateTimeOffset? ResumeAt { get; private init; }

    /// <summary>Gets the user-facing wait reason.</summary>
    public string? WaitReason { get; private init; }

    /// <summary>Gets the state persisted for a waiting step (handed back on resume).</summary>
    public JsonNode? WaitState { get; private init; }

    /// <summary>Creates a successful result.</summary>
    /// <param name="output">The output.</param>
    /// <param name="branch">The branch to execute next.</param>
    /// <returns>The result.</returns>
    public static FlowActionResult Success(JsonNode? output = null, string? branch = null) =>
        new(FlowActionOutcome.Succeeded) { Output = output, Branch = branch };

    /// <summary>Creates a failed result.</summary>
    /// <param name="error">The user-safe error message.</param>
    /// <param name="retryable">Whether the failure is transient.</param>
    /// <param name="output">Optional output (for example the HTTP status of a failed call).</param>
    /// <returns>The result.</returns>
    public static FlowActionResult Fail(string error, bool retryable = false, JsonNode? output = null) =>
        new(FlowActionOutcome.Failed) { Error = error, Retryable = retryable, Output = output };

    /// <summary>Creates a waiting result.</summary>
    /// <param name="resumeAt">When to check again.</param>
    /// <param name="reason">The user-facing reason.</param>
    /// <param name="state">State handed back on resume.</param>
    /// <returns>The result.</returns>
    public static FlowActionResult Wait(DateTimeOffset resumeAt, string reason, JsonNode? state = null) =>
        new(FlowActionOutcome.Waiting) { ResumeAt = resumeAt, WaitReason = reason, WaitState = state };
}
