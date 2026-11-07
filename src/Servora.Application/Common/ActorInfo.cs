namespace Servora.Application.Common;

/// <summary>Describes an explicitly set actor (system process, AI agent or impersonated principal).</summary>
/// <param name="ActorType">The actor type.</param>
/// <param name="Name">The technical name.</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="UserId">The user identifier, if the actor is (or acts for) a user.</param>
/// <param name="Permissions">
/// The granted permission keys. <see langword="null"/> means unrestricted (only allowed for <see cref="Common.ActorType.System"/>).
/// </param>
public sealed record ActorInfo(ActorType ActorType, string Name, string DisplayName, Guid? UserId = null, IReadOnlySet<string>? Permissions = null)
{
    /// <summary>Gets the unrestricted system actor used by background processing.</summary>
    public static ActorInfo System { get; } = new(ActorType.System, "system", "System");

    /// <summary>Creates a system actor with a descriptive name, for example <c>Flow: Escalate P1</c>.</summary>
    /// <param name="displayName">The display name.</param>
    /// <returns>The actor.</returns>
    public static ActorInfo SystemNamed(string displayName) => new(ActorType.System, "system", displayName);
}
