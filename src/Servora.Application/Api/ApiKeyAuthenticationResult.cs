namespace Servora.Application.Api;

/// <summary>Outcome of authenticating a presented API key.</summary>
/// <param name="Succeeded">Whether the key is valid for this request.</param>
/// <param name="Failure">A short, client-safe failure reason (never reveals whether a key id exists).</param>
/// <param name="Identity">The key identity on success.</param>
public sealed record ApiKeyAuthenticationResult(bool Succeeded, string? Failure, ApiKeyIdentity? Identity)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="identity">The identity.</param>
    /// <returns>The result.</returns>
    public static ApiKeyAuthenticationResult Success(ApiKeyIdentity identity) => new(true, null, identity);

    /// <summary>Creates a failed result.</summary>
    /// <param name="failure">The failure reason.</param>
    /// <returns>The result.</returns>
    public static ApiKeyAuthenticationResult Fail(string failure) => new(false, failure, null);
}
