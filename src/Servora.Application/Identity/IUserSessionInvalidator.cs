namespace Servora.Application.Identity;

/// <summary>
/// Notifies the host that the security stamp of a user changed (password reset, deactivation, forced sign-out), so
/// cached cookie / circuit validation state is dropped immediately instead of after the cache period.
/// </summary>
public interface IUserSessionInvalidator
{
    /// <summary>Drops cached session validation state of a user.</summary>
    /// <param name="userId">The user identifier.</param>
    void Invalidate(Guid userId);
}
