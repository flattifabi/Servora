namespace Servora.Application.Api;

/// <summary>A syntactically valid API key token split into its parts.</summary>
/// <param name="KeyId">The public key identifier.</param>
/// <param name="Secret">The secret part.</param>
public sealed record ParsedApiKeyToken(string KeyId, string Secret)
{
    /// <summary>Returns a redacted representation so the secret never ends up in logs by accident.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"ParsedApiKeyToken {{ KeyId = {KeyId}, Secret = *** }}";
}
