using System.Text.Json.Nodes;
using Servora.Domain.Automation;

namespace Servora.Application.Automation.Engine;

/// <summary>Internal request to create and queue a flow run (permissions are checked by the caller).</summary>
public sealed record FlowStartRequest
{
    /// <summary>Gets the flow.</summary>
    public required Guid FlowId { get; init; }

    /// <summary>Gets what started the run.</summary>
    public required FlowTriggerType TriggerType { get; init; }

    /// <summary>Gets the idempotency key (a random key is used when empty).</summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>Gets the trigger data (redacted before storage).</summary>
    public JsonObject? Trigger { get; init; }

    /// <summary>Gets the validated input values.</summary>
    public JsonObject? Inputs { get; init; }

    /// <summary>Gets a short, secret-free trigger description.</summary>
    public string? Summary { get; init; }

    /// <summary>Gets the table of the triggering record.</summary>
    public string? TriggerTable { get; init; }

    /// <summary>Gets the triggering record.</summary>
    public Guid? TriggerRecordId { get; init; }

    /// <summary>Gets the parent run for sub-flows.</summary>
    public Guid? ParentRunId { get; init; }

    /// <summary>Gets the sub-flow depth.</summary>
    public int Depth { get; init; }

    /// <summary>Gets the record event cascade depth inherited by records the run changes.</summary>
    public int EventDepth { get; init; }

    /// <summary>Gets a value indicating whether this is a designer test run (inactive flows allowed).</summary>
    public bool IsTest { get; init; }

    /// <summary>Gets the display name of the starting actor.</summary>
    public string? StartedBy { get; init; }
}
