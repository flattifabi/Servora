namespace Servora.Application.Analytics;

/// <summary>A role a report can be shared with.</summary>
/// <param name="Name">The technical role name.</param>
/// <param name="Label">The display label.</param>
public sealed record ShareableRole(string Name, string Label);
