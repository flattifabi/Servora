namespace Servora.Application.Identity.Policies;

/// <summary>
/// Backend read access to the effective authentication policy (used by the sign-in pipeline, the password change
/// and the cookie session configuration). Not a UI service: it performs no permission check and returns no secrets.
/// </summary>
public interface IAuthPolicyProvider
{
    /// <summary>Gets the effective authentication policy (cached briefly).</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policy.</returns>
    Task<AuthPolicySettings> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets the effective password policy.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The password policy.</returns>
    Task<PasswordPolicy> GetPasswordPolicyAsync(CancellationToken cancellationToken = default);
}
