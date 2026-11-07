namespace Servora.Infrastructure.Identity;

/// <summary>Definition of a built-in role seeded at startup.</summary>
/// <param name="Name">The technical role name.</param>
/// <param name="Label">The display label.</param>
/// <param name="Description">The description.</param>
/// <param name="Permissions">The default permission keys.</param>
public sealed record BuiltInRoleDefinition(string Name, string Label, string Description, IReadOnlyList<string> Permissions);
