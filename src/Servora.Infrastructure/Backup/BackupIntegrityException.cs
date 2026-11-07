namespace Servora.Infrastructure.Backup;

/// <summary>The archive is malformed, truncated or has been tampered with.</summary>
public sealed class BackupIntegrityException : BackupException
{
    /// <summary>Initializes a new instance of the <see cref="BackupIntegrityException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BackupIntegrityException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BackupIntegrityException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BackupIntegrityException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
