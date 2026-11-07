namespace Servora.Application.Identity;

/// <summary>Well-known facts about the bootstrap administrator account (ADR-006).</summary>
public static class BootstrapAccount
{
    /// <summary>Login name of the bootstrap administrator. It keeps local (break-glass) sign-in even when local sign-in is disabled.</summary>
    public const string AdminUserName = "admin";

    /// <summary>Technical name of the built-in administrator role. It always keeps the wildcard permission <c>*</c>.</summary>
    public const string AdminRoleName = "admin";

    /// <summary>Checks whether a login name is the bootstrap administrator.</summary>
    /// <param name="userName">The login name.</param>
    /// <returns><see langword="true"/> for the bootstrap administrator.</returns>
    public static bool IsAdmin(string? userName) => string.Equals(userName, AdminUserName, StringComparison.OrdinalIgnoreCase);
}
