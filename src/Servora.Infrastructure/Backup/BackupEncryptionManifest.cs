namespace Servora.Infrastructure.Backup;

/// <summary>Encryption parameters of an encrypted archive. Contains no secret material.</summary>
public sealed class BackupEncryptionManifest
{
    /// <summary>Gets or sets the content encryption algorithm.</summary>
    public string Algorithm { get; set; } = "AES-256-GCM";

    /// <summary>Gets or sets the key derivation function.</summary>
    public string Kdf { get; set; } = "PBKDF2-SHA256";

    /// <summary>Gets or sets the PBKDF2 iteration count.</summary>
    public int Iterations { get; set; }

    /// <summary>Gets or sets the base64 salt.</summary>
    public string Salt { get; set; } = string.Empty;

    /// <summary>Gets or sets the base64 key check value (HMAC of a constant) used to detect a wrong password.</summary>
    public string KeyCheck { get; set; } = string.Empty;

    /// <summary>Gets or sets the plaintext chunk size of the encrypted entry streams.</summary>
    public int ChunkSize { get; set; }
}
