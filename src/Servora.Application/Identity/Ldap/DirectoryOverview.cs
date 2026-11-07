namespace Servora.Application.Identity.Ldap;

/// <summary>Read model of the directory administration page.</summary>
/// <param name="Settings">The saved settings.</param>
/// <param name="HasBindPassword">Whether a bind password is stored (the value itself is never returned).</param>
/// <param name="DirectoryUserCount">Number of local users mastered by the directory.</param>
/// <param name="WindowsUserCount">Number of local users provisioned by Windows sign-in.</param>
/// <param name="MappingCount">Number of group mappings.</param>
/// <param name="LastRun">The last synchronisation run.</param>
public sealed record DirectoryOverview(
    DirectorySettings Settings,
    bool HasBindPassword,
    int DirectoryUserCount,
    int WindowsUserCount,
    int MappingCount,
    DirectorySyncSummary? LastRun);
