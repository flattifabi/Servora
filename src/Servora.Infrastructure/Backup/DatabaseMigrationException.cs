namespace Servora.Infrastructure.Backup;

/// <summary>
/// Startup must stop because the database schema cannot be brought up to date safely (legacy database, database newer
/// than the application, failed pre-migration backup or failed migration). The message tells the operator what to do.
/// </summary>
public sealed class DatabaseMigrationException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="DatabaseMigrationException"/> class.</summary>
    /// <param name="message">The operator-facing message.</param>
    public DatabaseMigrationException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DatabaseMigrationException"/> class.</summary>
    /// <param name="message">The operator-facing message.</param>
    /// <param name="innerException">The cause.</param>
    public DatabaseMigrationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
