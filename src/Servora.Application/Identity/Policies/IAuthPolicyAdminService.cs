using Servora.Application.Common;

namespace Servora.Application.Identity.Policies;

/// <summary>Administration of the authentication policy (<c>/admin/auth</c>). Requires <c>admin.auth.manage</c>.</summary>
public interface IAuthPolicyAdminService
{
    /// <summary>Gets the current policy.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The policy.</returns>
    Task<Result<AuthPolicySettings>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and saves the policy. Changes are audited and take effect within seconds.</summary>
    /// <param name="settings">The new policy.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SaveAsync(AuthPolicySettings settings, CancellationToken cancellationToken = default);
}
