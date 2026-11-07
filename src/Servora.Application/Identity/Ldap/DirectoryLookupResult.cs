namespace Servora.Application.Identity.Ldap;

/// <summary>Result of <see cref="IDirectoryClient.FindUserAsync"/> and <see cref="IDirectoryClient.AuthenticateAsync"/>.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="Entry">The user entry on success (and for <see cref="DirectoryLookupOutcome.InvalidPassword"/>).</param>
/// <param name="Message">A technical, secret-free message for administrators and logs.</param>
public sealed record DirectoryLookupResult(DirectoryLookupOutcome Outcome, DirectoryUserEntry? Entry = null, string? Message = null)
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Succeeded => Outcome == DirectoryLookupOutcome.Success && Entry is not null;
}
