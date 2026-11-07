namespace Servora.Application.Identity.Ldap;

/// <summary>A directory group mapping row for the administration UI.</summary>
/// <param name="Id">The mapping identifier.</param>
/// <param name="DirectoryGroup">The directory group DN or common name.</param>
/// <param name="RoleId">The mapped role.</param>
/// <param name="RoleLabel">The mapped role label.</param>
/// <param name="GroupId">The mapped local group.</param>
/// <param name="GroupName">The mapped local group name.</param>
/// <param name="Description">The description.</param>
/// <param name="IsActive">Whether the mapping is active.</param>
public sealed record DirectoryMappingItem(
    Guid Id,
    string DirectoryGroup,
    Guid? RoleId,
    string? RoleLabel,
    Guid? GroupId,
    string? GroupName,
    string? Description,
    bool IsActive);
