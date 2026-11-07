namespace Servora.Application.Identity.Windows;

/// <summary>
/// Maps an authenticated Windows identity (Negotiate / Kerberos / NTLM) to a Servora user: by SID for Windows accounts,
/// by directory lookup (and just-in-time provisioning) when LDAP is enabled, or by creating a Windows account when
/// auto-provisioning is on. Local accounts are never matched. Applies the same active / lockout checks and auditing
/// as password sign-in.
/// </summary>
public interface IWindowsSignInService
{
    /// <summary>Gets a value indicating whether Windows sign-in is switched on in the directory settings.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when enabled.</returns>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves the Servora user for a Windows identity.</summary>
    /// <param name="windowsName">The Windows account name, typically <c>DOMAIN\user</c> (or <c>user@REALM</c>).</param>
    /// <param name="sid">The primary SID (<c>S-1-5-21-…</c>) when available.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sign-in result with auth source <c>windows</c>.</returns>
    Task<SignInResult> SignInAsync(string windowsName, string? sid, CancellationToken cancellationToken = default);
}
