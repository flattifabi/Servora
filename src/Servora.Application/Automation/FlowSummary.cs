namespace Servora.Application.Automation;

/// <summary>A flow that can be started manually (UI, business rule, API, MCP).</summary>
/// <param name="Id">The flow identifier.</param>
/// <param name="Name">The name.</param>
/// <param name="Description">The description.</param>
/// <param name="IsActive">Whether the flow is active.</param>
public sealed record FlowSummary(Guid Id, string Name, string? Description, bool IsActive);
