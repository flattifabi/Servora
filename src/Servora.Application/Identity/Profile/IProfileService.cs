using Servora.Application.Common;

namespace Servora.Application.Identity.Profile;

/// <summary>
/// Self-service profile of the signed-in interactive user. Every method requires an authenticated user actor and only
/// ever touches the actor's own record (no permission key needed).
/// </summary>
public interface IProfileService
{
    /// <summary>Gets the own profile.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The profile.</returns>
    Task<Result<MyProfile>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates the own profile.</summary>
    /// <param name="update">The new values.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(MyProfileUpdate update, CancellationToken cancellationToken = default);

    /// <summary>Signs the user out of every session (including the current one) by rotating the security stamp.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SignOutEverywhereAsync(CancellationToken cancellationToken = default);
}
