namespace Servora.Application.Identity.Ldap;

/// <summary>Result of "Test user lookup": the directory entry and what Servora would grant.</summary>
/// <param name="Entry">The directory entry.</param>
/// <param name="MappedRoles">Labels of roles granted through group mappings.</param>
/// <param name="MappedGroups">Names of local groups granted through group mappings.</param>
/// <param name="ExistingUserId">The linked local user, if one exists.</param>
/// <param name="Note">A hint, for example that a local account with the same name blocks linking.</param>
public sealed record DirectoryUserPreview(
    DirectoryUserEntry Entry,
    IReadOnlyList<string> MappedRoles,
    IReadOnlyList<string> MappedGroups,
    Guid? ExistingUserId,
    string? Note);
