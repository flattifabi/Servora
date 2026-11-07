using Servora.Application.Common;

namespace Servora.Application.Identity;

/// <summary>Validates interactive credentials (local accounts and external directories) and manages passwords.</summary>
public interface ISignInService
{
    /// <summary>
    /// Validates credentials. Applies lockout, records the sign-in time and writes audit events for success and failure.
    /// </summary>
    /// <param name="userName">The user name.</param>
    /// <param name="password">The password.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The sign-in result.</returns>
    Task<SignInResult> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default);

    /// <summary>Changes the password of a local account after verifying the current password.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="currentPassword">The current password.</param>
    /// <param name="newPassword">The new password (validated against the password policy).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
