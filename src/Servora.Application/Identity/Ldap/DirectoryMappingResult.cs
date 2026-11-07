namespace Servora.Application.Identity.Ldap;

/// <summary>Roles and local groups a directory user is entitled to through <c>DirectoryGroupMapping</c>s.</summary>
/// <param name="RoleIds">The mapped role identifiers.</param>
/// <param name="GroupIds">The mapped local group identifiers.</param>
public sealed record DirectoryMappingResult(IReadOnlySet<Guid> RoleIds, IReadOnlySet<Guid> GroupIds);
