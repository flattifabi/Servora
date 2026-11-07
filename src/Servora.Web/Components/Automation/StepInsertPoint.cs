using Servora.Application.Automation.Model;

namespace Servora.Web.Components.Automation;

/// <summary>A position in a step list of the Flow Designer canvas (root list or a branch lane).</summary>
/// <param name="Steps">The list the position belongs to.</param>
/// <param name="Index">The index inside the list.</param>
public sealed record StepInsertPoint(List<FlowStepDefinition> Steps, int Index);
