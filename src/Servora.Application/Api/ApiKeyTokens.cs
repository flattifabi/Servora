using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace Servora.Application.Api;

/// <summary>
/// Generates, parses and verifies API key tokens. Format: <c>srv_&lt;key id&gt;_&lt;secret&gt;</c> where the key id
/// (12 lower-case alphanumerics) is stored in clear text for lookup and only a SHA-256 hash of the secret
/// (40 alphanumerics, ~238 bits of entropy) is persisted. Verification uses a constant-time comparison.
/// </summary>
public static class ApiKeyTokens
{
    /// <summary>The token prefix.</summary>
    public const string Prefix = "srv_";

    /// <summary>The length of the public key identifier.</summary>
    public const int KeyIdLength = 12;

    /// <summary>The length of the secret part.</summary>
    public const int SecretLength = 40;

    /// <summary>The maximum accepted token length (defensive bound before any processing).</summary>
    public const int MaxTokenLength = 128;

    private const string KeyIdAlphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
    private const string SecretAlphabet = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

    /// <summary>Generates a new token with a random key id and secret.</summary>
    /// <returns>The generated token including the secret hash to persist.</returns>
    public static GeneratedApiKeyToken Generate() => GenerateForKeyId(RandomNumberGenerator.GetString(KeyIdAlphabet, KeyIdLength));

    /// <summary>Generates a new secret for an existing key id (rotation).</summary>
    /// <param name="keyId">The existing key id.</param>
    /// <returns>The generated token.</returns>
    public static GeneratedApiKeyToken GenerateForKeyId(string keyId)
    {
        var secret = RandomNumberGenerator.GetString(SecretAlphabet, SecretLength);
        return new GeneratedApiKeyToken(keyId, $"{Prefix}{keyId}_{secret}", HashSecret(secret));
    }

    /// <summary>Parses a token into key id and secret without touching the database.</summary>
    /// <param name="token">The raw token.</param>
    /// <param name="parsed">The parsed token.</param>
    /// <returns><see langword="true"/> when the token is well-formed.</returns>
    public static bool TryParse(string? token, [NotNullWhen(true)] out ParsedApiKeyToken? parsed)
    {
        parsed = null;
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength || !token.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = token.AsSpan(Prefix.Length);
        var separator = rest.IndexOf('_');
        if (separator != KeyIdLength)
        {
            return false;
        }

        var keyId = rest[..separator];
        var secret = rest[(separator + 1)..];
        if (secret.Length != SecretLength || !IsFrom(keyId, KeyIdAlphabet) || !IsFrom(secret, SecretAlphabet))
        {
            return false;
        }

        parsed = new ParsedApiKeyToken(keyId.ToString(), secret.ToString());
        return true;
    }

    /// <summary>Extracts the key id of a well-formed token (used for rate limit partitioning before authentication).</summary>
    /// <param name="token">The raw token.</param>
    /// <returns>The key id, or <see langword="null"/>.</returns>
    public static string? TryGetKeyId(string? token) => TryParse(token, out var parsed) ? parsed.KeyId : null;

    /// <summary>Computes the lower-case hex SHA-256 hash of a secret.</summary>
    /// <param name="secret">The secret.</param>
    /// <returns>The hash.</returns>
    public static string HashSecret(string secret) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(secret)));

    /// <summary>Verifies a secret against a stored hash with a constant-time comparison.</summary>
    /// <param name="secret">The presented secret.</param>
    /// <param name="expectedHash">The stored lower-case hex hash.</param>
    /// <returns><see langword="true"/> when the secret matches.</returns>
    public static bool Verify(string secret, string expectedHash)
    {
        if (string.IsNullOrEmpty(secret) || expectedHash is not { Length: 64 })
        {
            return false;
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(expectedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>Builds the masked display form of a key, for example <c>srv_ab12cd34ef56_••••</c>.</summary>
    /// <param name="keyId">The key id.</param>
    /// <returns>The masked key.</returns>
    public static string Mask(string keyId) => $"{Prefix}{keyId}_••••••••";

    private static bool IsFrom(ReadOnlySpan<char> value, string alphabet)
    {
        foreach (var c in value)
        {
            if (!alphabet.Contains(c))
            {
                return false;
            }
        }

        return true;
    }
}
