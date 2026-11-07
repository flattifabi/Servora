using Servora.Domain.Identity;

namespace Servora.Application.Identity;

/// <summary>Result of <see cref="ISignInService.ValidateCredentialsAsync"/>.</summary>
/// <param name="Outcome">The outcome.</param>
/// <param name="User">The authenticated user on success.</param>
/// <param name="AuthSource">The authentication source (<c>local</c>, <c>directory</c>).</param>
public sealed record SignInResult(SignInOutcome Outcome, User? User = null, string AuthSource = "local")
{
    /// <summary>Gets a value indicating whether the sign-in succeeded.</summary>
    public bool Succeeded => Outcome == SignInOutcome.Success && User is not null;

    /// <summary>Gets a generic, user-safe message for the outcome (does not reveal whether the user exists).</summary>
    public string Message => Outcome switch
    {
        SignInOutcome.Success => "Signed in.",
        SignInOutcome.LockedOut => "The account is temporarily locked. Try again later or contact your administrator.",
        SignInOutcome.Disabled => "The account is disabled. Contact your administrator.",
        SignInOutcome.DirectoryUnavailable => "The directory service is currently unavailable. Try again later or use a local account.",
        _ => "Invalid user name or password.",
    };
}
