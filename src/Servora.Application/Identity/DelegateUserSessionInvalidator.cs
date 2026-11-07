namespace Servora.Application.Identity;

/// <summary><see cref="IUserSessionInvalidator"/> that forwards to a delegate supplied by the host.</summary>
/// <param name="invalidate">The delegate invoked with the user identifier.</param>
public sealed class DelegateUserSessionInvalidator(Action<Guid> invalidate) : IUserSessionInvalidator
{
    /// <inheritdoc />
    public void Invalidate(Guid userId) => invalidate(userId);
}
