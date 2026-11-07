using Servora.Domain.Automation;

namespace Servora.Application.Automation.Services;

/// <summary>Request to create a new (inactive) flow.</summary>
/// <param name="Name">The unique name.</param>
/// <param name="Description">The description.</param>
/// <param name="TriggerType">The trigger type.</param>
/// <param name="TriggerTable">The table for record triggers.</param>
public sealed record FlowCreateRequest(string Name, string? Description, FlowTriggerType TriggerType, string? TriggerTable);
