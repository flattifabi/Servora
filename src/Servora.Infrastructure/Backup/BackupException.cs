namespace Servora.Infrastructure.Backup;

/// <summary>An expected backup or restore failure with an operator-facing message (never containing secrets).</summary>
public class BackupException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="BackupException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BackupException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="BackupException"/> class.</summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The cause.</param>
    public BackupException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
