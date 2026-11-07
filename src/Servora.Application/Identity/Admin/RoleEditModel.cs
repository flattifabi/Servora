namespace Servora.Application.Identity.Admin;

/// <summary>Input model for creating or editing a role.</summary>
public sealed class RoleEditModel
{
    /// <summary>Gets or sets the technical name (lower case, immutable for system roles).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets the granted permission keys.</summary>
    public List<string> Permissions { get; set; } = [];
}
