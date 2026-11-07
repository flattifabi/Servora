namespace Servora.Application.Api;

/// <summary>A freshly generated API key token. The <see cref="Token"/> must be shown once and never stored.</summary>
/// <param name="KeyId">The public key identifier.</param>
/// <param name="Token">The full plain token (<c>srv_&lt;key id&gt;_&lt;secret&gt;</c>).</param>
/// <param name="SecretHash">The SHA-256 hash of the secret to persist.</param>
public sealed record GeneratedApiKeyToken(string KeyId, string Token, string SecretHash)
{
    /// <summary>Returns a redacted representation so the token never ends up in logs by accident.</summary>
    /// <returns>The redacted string.</returns>
    public override string ToString() => $"GeneratedApiKeyToken {{ KeyId = {KeyId}, Token = *** }}";
}
