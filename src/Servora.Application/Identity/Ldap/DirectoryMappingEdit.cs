namespace Servora.Application.Identity.Ldap;

/// <summary>Input model for creating or editing a directory group mapping.</summary>
public sealed class DirectoryMappingEdit
{
    /// <summary>Gets or sets the mapping identifier (<see langword="null"/> for a new mapping).</summary>
    public Guid? Id { get; set; }

    /// <summary>Gets or sets the directory group DN or common name.</summary>
    public string DirectoryGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the mapped role.</summary>
    public Guid? RoleId { get; set; }

    /// <summary>Gets or sets the mapped local group.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string? Description { get; set; }

    /// <summary>Gets or sets a value indicating whether the mapping is active.</summary>
    public bool IsActive { get; set; } = true;
}
