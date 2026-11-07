using Servora.Domain.Identity;

namespace Servora.Application.Identity;

/// <summary>
/// Validates credentials against an external directory (LDAP / Active Directory) and provisions or updates the local
/// user record. Implemented by the Identity module; multiple validators are tried in registration order.
/// </summary>
public interface IExternalCredentialValidator
{
    /// <summary>Gets the authentication source key written to the sign-in claims, for example <c>directory</c>.</summary>
    string Source { get; }

    /// <summary>Gets a value indicating whether the validator is enabled (configured and switched on).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when enabled.</returns>
    Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates the credentials and returns the provisioned local user.</summary>
    /// <param name="userName">The user name as typed (may include a domain prefix or UPN suffix).</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result; <see cref="SignInOutcome.InvalidCredentials"/> when the user is unknown to the directory.</returns>
    Task<(SignInOutcome Outcome, User? User)> ValidateAsync(string userName, string password, CancellationToken cancellationToken = default);
}
