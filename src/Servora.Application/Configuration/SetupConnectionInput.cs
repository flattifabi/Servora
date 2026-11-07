namespace Servora.Application.Configuration;

/// <summary>Connection builder fields of the setup wizard. Mutable so it can be bound to a form.</summary>
public sealed class SetupConnectionInput
{
    /// <summary>Gets or sets the provider key (see <see cref="DatabaseProviderKeys"/>).</summary>
    public string Provider { get; set; } = DatabaseProviderKeys.Sqlite;

    /// <summary>Gets or sets a value indicating whether <see cref="RawConnectionString"/> is used instead of the builder fields.</summary>
    public bool UseRawConnectionString { get; set; }

    /// <summary>Gets or sets the raw connection string.</summary>
    public string? RawConnectionString { get; set; }

    /// <summary>Gets or sets the SQLite database file (absolute or relative to the data directory).</summary>
    public string FilePath { get; set; } = "servora.db";

    /// <summary>Gets or sets the server host name (SQL Server instance, for example <c>sql01\SERVORA</c>, or PostgreSQL host).</summary>
    public string Server { get; set; } = "localhost";

    /// <summary>Gets or sets the TCP port; <see langword="null"/> uses the provider default.</summary>
    public int? Port { get; set; }

    /// <summary>Gets or sets the database name.</summary>
    public string Database { get; set; } = "servora";

    /// <summary>Gets or sets a value indicating whether Windows integrated authentication is used (SQL Server only).</summary>
    public bool IntegratedSecurity { get; set; }

    /// <summary>Gets or sets the user name.</summary>
    public string UserName { get; set; } = string.Empty;

    /// <summary>Gets or sets the password.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the connection is encrypted (SQL Server).</summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether the server certificate is trusted without validation (SQL Server; not recommended).</summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>Gets or sets the PostgreSQL SSL mode: <c>Disable</c>, <c>Prefer</c>, <c>Require</c>, <c>VerifyCA</c>, <c>VerifyFull</c>.</summary>
    public string SslMode { get; set; } = "Prefer";
}
