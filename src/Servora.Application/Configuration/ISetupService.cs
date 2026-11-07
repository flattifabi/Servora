using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Backend of the first-run setup wizard. Works while no database is configured. Callers (the wizard) must verify that
/// the client is allowed to run setup (loopback or valid setup token) before calling any operation.
/// </summary>
public interface ISetupService
{
    /// <summary>Gets a value indicating whether a database is configured.</summary>
    bool IsConfigured { get; }

    /// <summary>Gets a value indicating whether an installation of this process saved the configuration but did not finish (retry allowed).</summary>
    bool HasIncompleteInstallation { get; }

    /// <summary>Gets the data directory (shown as default location of the SQLite file).</summary>
    string DataDirectory { get; }

    /// <summary>Gets the full path of the setup token file.</summary>
    string SetupTokenPath { get; }

    /// <summary>Gets the supported database providers.</summary>
    /// <returns>The providers.</returns>
    IReadOnlyList<SetupProviderOption> GetProviders();

    /// <summary>Composes (and validates) the connection string from the wizard fields.</summary>
    /// <param name="input">The builder fields.</param>
    /// <returns>The connection string or a validation error.</returns>
    Result<string> ComposeConnectionString(SetupConnectionInput input);

    /// <summary>Returns the connection string with credentials masked.</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The redacted connection string.</returns>
    string Redact(string provider, string connectionString);

    /// <summary>Tests a connection (15 s timeout).</summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The round-trip time or a user-safe error.</returns>
    Task<Result<TimeSpan>> TestConnectionAsync(string provider, string connectionString, CancellationToken cancellationToken = default);

    /// <summary>Validates a setup token in constant time.</summary>
    /// <param name="token">The token entered by the user.</param>
    /// <returns><see langword="true"/> when the token matches.</returns>
    bool ValidateSetupToken(string? token);

    /// <summary>
    /// Installs Servora on the given database: validates the connection, creates the schema, saves the encrypted
    /// configuration, seeds data (including <c>admin</c> / <c>admin</c> with forced password change), applies the
    /// organisation settings and removes the setup token.
    /// </summary>
    /// <param name="provider">The provider key.</param>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="organization">The organisation settings.</param>
    /// <param name="progress">Receives step updates.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> InstallAsync(string provider, string connectionString, SetupOrganizationInput organization, IProgress<SetupProgressUpdate>? progress, CancellationToken cancellationToken = default);
}
