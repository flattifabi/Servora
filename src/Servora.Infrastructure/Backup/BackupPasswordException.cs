namespace Servora.Infrastructure.Backup;

/// <summary>The archive is encrypted and the password is missing or wrong.</summary>
public sealed class BackupPasswordException : BackupException
{
    /// <summary>Initializes a new instance of the <see cref="BackupPasswordException"/> class.</summary>
    /// <param name="message">The message.</param>
    public BackupPasswordException(string message)
        : base(message)
    {
    }
}
