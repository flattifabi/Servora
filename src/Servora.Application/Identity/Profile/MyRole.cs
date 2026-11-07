namespace Servora.Application.Identity.Profile;

/// <summary>A role of the signed-in user.</summary>
/// <param name="Label">The role label.</param>
/// <param name="Description">The role description.</param>
/// <param name="Origin">Where the role comes from: <c>Direct</c>, <c>Directory</c> or the granting group name.</param>
public sealed record MyRole(string Label, string? Description, string Origin);
