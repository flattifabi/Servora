using System.Security.Cryptography;
using System.Text;

namespace Servora.Infrastructure.Backup;

/// <summary>
/// Password based archive encryption: PBKDF2-SHA256 derives 64 bytes from the password and a random salt; the first 32
/// bytes are the AES-256-GCM content key, the last 32 bytes the HMAC-SHA256 key used for the key check value and the
/// manifest signature. Entries are encrypted in authenticated chunks (<see cref="AesGcmChunkWriteStream"/>) whose nonce
/// combines the entry index and the chunk counter; because every archive has its own salt (and therefore key), nonces
/// never repeat under the same key.
/// </summary>
public sealed class BackupCipher : IDisposable
{
    /// <summary>The default PBKDF2 iteration count (OWASP recommendation for PBKDF2-HMAC-SHA256).</summary>
    public const int DefaultIterations = 600_000;

    /// <summary>The smallest iteration count accepted when reading an archive.</summary>
    public const int MinIterations = 10_000;

    /// <summary>The largest iteration count accepted when reading an archive (prevents denial of service).</summary>
    public const int MaxIterations = 10_000_000;

    /// <summary>The default plaintext chunk size.</summary>
    public const int DefaultChunkSize = 64 * 1024;

    /// <summary>The largest chunk size accepted when reading an archive.</summary>
    public const int MaxChunkSize = 4 * 1024 * 1024;

    private static readonly byte[] s_keyCheckLabel = Encoding.ASCII.GetBytes("servora-backup-key-check-v1");
    private readonly byte[] _encryptionKey;
    private readonly byte[] _macKey;

    private BackupCipher(byte[] encryptionKey, byte[] macKey, BackupEncryptionManifest parameters)
    {
        _encryptionKey = encryptionKey;
        _macKey = macKey;
        Parameters = parameters;
    }

    /// <summary>Gets the parameters to store in the manifest.</summary>
    public BackupEncryptionManifest Parameters { get; }

    /// <summary>Creates a cipher for a new archive with a random salt.</summary>
    /// <param name="password">The password.</param>
    /// <param name="iterations">The PBKDF2 iteration count.</param>
    /// <param name="chunkSize">The plaintext chunk size.</param>
    /// <returns>The cipher.</returns>
    public static BackupCipher Create(string password, int iterations = DefaultIterations, int chunkSize = DefaultChunkSize)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        var salt = RandomNumberGenerator.GetBytes(16);
        var (encryptionKey, macKey) = Derive(password, salt, iterations);
        var parameters = new BackupEncryptionManifest
        {
            Iterations = iterations,
            Salt = Convert.ToBase64String(salt),
            KeyCheck = Convert.ToBase64String(HMACSHA256.HashData(macKey, s_keyCheckLabel)),
            ChunkSize = chunkSize,
        };
        return new BackupCipher(encryptionKey, macKey, parameters);
    }

    /// <summary>Re-creates the cipher of an existing archive and checks the password.</summary>
    /// <param name="password">The password.</param>
    /// <param name="parameters">The archive's encryption parameters.</param>
    /// <returns>The cipher.</returns>
    /// <exception cref="BackupPasswordException">Thrown when the password is wrong.</exception>
    /// <exception cref="BackupIntegrityException">Thrown when the parameters are invalid.</exception>
    public static BackupCipher Open(string password, BackupEncryptionManifest parameters)
    {
        if (parameters.Algorithm != "AES-256-GCM" || parameters.Kdf != "PBKDF2-SHA256")
        {
            throw new BackupIntegrityException($"Unsupported archive encryption '{parameters.Algorithm}' / '{parameters.Kdf}'.");
        }

        if (parameters.Iterations is < MinIterations or > MaxIterations || parameters.ChunkSize is < 1024 or > MaxChunkSize)
        {
            throw new BackupIntegrityException("The archive encryption parameters are out of range.");
        }

        byte[] salt;
        byte[] expectedCheck;
        try
        {
            salt = Convert.FromBase64String(parameters.Salt);
            expectedCheck = Convert.FromBase64String(parameters.KeyCheck);
        }
        catch (FormatException ex)
        {
            throw new BackupIntegrityException("The archive encryption parameters are malformed.", ex);
        }

        if (string.IsNullOrEmpty(password))
        {
            throw new BackupPasswordException("The backup is encrypted. A password is required.");
        }

        var (encryptionKey, macKey) = Derive(password, salt, parameters.Iterations);
        var actualCheck = HMACSHA256.HashData(macKey, s_keyCheckLabel);
        if (!CryptographicOperations.FixedTimeEquals(actualCheck, expectedCheck))
        {
            CryptographicOperations.ZeroMemory(encryptionKey);
            CryptographicOperations.ZeroMemory(macKey);
            throw new BackupPasswordException("The backup password is wrong.");
        }

        return new BackupCipher(encryptionKey, macKey, parameters);
    }

    /// <summary>Checks a password against the parameters of an archive without throwing.</summary>
    /// <param name="password">The password.</param>
    /// <param name="parameters">The archive's encryption parameters.</param>
    /// <returns><see langword="true"/> when the password opens the archive.</returns>
    public static bool IsPasswordValid(string? password, BackupEncryptionManifest parameters)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        try
        {
            using var cipher = Open(password, parameters);
            return true;
        }
        catch (BackupException)
        {
            return false;
        }
    }

    /// <summary>Wraps an output stream so that everything written is encrypted.</summary>
    /// <param name="output">The output stream (left open).</param>
    /// <param name="entryIndex">The entry index.</param>
    /// <param name="entryPath">The entry path (authenticated).</param>
    /// <returns>The encrypting stream; dispose it to write the final chunk.</returns>
    public Stream CreateEncryptor(Stream output, int entryIndex, string entryPath) =>
        new AesGcmChunkWriteStream(output, _encryptionKey, entryIndex, entryPath, Parameters.ChunkSize);

    /// <summary>Wraps an input stream so that reading returns the decrypted content.</summary>
    /// <param name="input">The encrypted input stream (left open).</param>
    /// <param name="entryIndex">The entry index.</param>
    /// <param name="entryPath">The entry path (authenticated).</param>
    /// <returns>The decrypting stream.</returns>
    public Stream CreateDecryptor(Stream input, int entryIndex, string entryPath) =>
        new AesGcmChunkReadStream(input, _encryptionKey, entryIndex, entryPath, Parameters.ChunkSize);

    /// <summary>Computes the manifest signature.</summary>
    /// <param name="data">The manifest bytes.</param>
    /// <returns>The signature.</returns>
    public byte[] Sign(ReadOnlySpan<byte> data) => HMACSHA256.HashData(_macKey, data);

    /// <summary>Verifies a manifest signature in constant time.</summary>
    /// <param name="data">The manifest bytes.</param>
    /// <param name="signature">The signature to check.</param>
    /// <returns><see langword="true"/> when the signature matches.</returns>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature) => CryptographicOperations.FixedTimeEquals(Sign(data), signature);

    /// <inheritdoc />
    public void Dispose()
    {
        CryptographicOperations.ZeroMemory(_encryptionKey);
        CryptographicOperations.ZeroMemory(_macKey);
    }

    private static (byte[] EncryptionKey, byte[] MacKey) Derive(string password, byte[] salt, int iterations)
    {
        var material = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, iterations, HashAlgorithmName.SHA256, 64);
        var encryptionKey = material[..32];
        var macKey = material[32..];
        CryptographicOperations.ZeroMemory(material);
        return (encryptionKey, macKey);
    }
}
