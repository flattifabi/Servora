namespace Servora.Application.Configuration;

/// <summary>Keys of the supported database providers (match <c>IDatabaseProvider.Key</c>).</summary>
public static class DatabaseProviderKeys
{
    /// <summary>SQLite.</summary>
    public const string Sqlite = "Sqlite";

    /// <summary>Microsoft SQL Server / Azure SQL.</summary>
    public const string SqlServer = "SqlServer";

    /// <summary>PostgreSQL.</summary>
    public const string PostgreSql = "PostgreSql";
}
