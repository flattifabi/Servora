namespace Servora.Application.Portal;

/// <summary>One step of the status timeline of a portal item.</summary>
/// <param name="Label">The step label, for example <c>Submitted</c>.</param>
/// <param name="Description">A short explanation for the requester.</param>
/// <param name="State">The state of the step.</param>
/// <param name="At">The UTC time the step was reached, when known.</param>
public sealed record PortalProgressStep(string Label, string? Description, PortalStepState State, DateTimeOffset? At = null);
