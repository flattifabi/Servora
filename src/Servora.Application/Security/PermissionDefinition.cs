namespace Servora.Application.Security;

/// <summary>Describes a permission key for display in the role editor.</summary>
/// <param name="Key">The permission key.</param>
/// <param name="Group">The functional group (module) shown as a section header.</param>
/// <param name="Description">The human readable description.</param>
public sealed record PermissionDefinition(string Key, string Group, string Description);
