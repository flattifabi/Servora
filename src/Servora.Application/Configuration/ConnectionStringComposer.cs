using System.Data.Common;
using System.Globalization;
using Servora.Application.Common;

namespace Servora.Application.Configuration;

/// <summary>
/// Builds provider connection strings from the setup wizard's builder fields. Uses <see cref="DbConnectionStringBuilder"/>
/// so values containing separators or quotes (for example passwords with <c>;</c>) are escaped correctly.
/// </summary>
public static class ConnectionStringComposer
{
    /// <summary>The data directory placeholder accepted in SQLite paths and raw connection strings.</summary>
    public const string DataDirectoryToken = "{DataDirectory}";

    /// <summary>The maximum accepted connection string length.</summary>
    public const int MaxLength = 4000;

    private static readonly string[] s_sslModes = ["Disable", "Prefer", "Require", "VerifyCA", "VerifyFull"];

    /// <summary>Gets the PostgreSQL SSL modes offered by the wizard.</summary>
    public static IReadOnlyList<string> PostgreSqlSslModes => s_sslModes;

    /// <summary>Composes the connection string.</summary>
    /// <param name="input">The builder fields.</param>
    /// <param name="dataDirectory">The data directory used to resolve relative SQLite paths and the <see cref="DataDirectoryToken"/>.</param>
    /// <returns>The connection string or a validation error.</returns>
    public static Result<string> Compose(SetupConnectionInput input, string dataDirectory)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.UseRawConnectionString)
        {
            return ValidateRaw(input.RawConnectionString, dataDirectory);
        }

        return input.Provider switch
        {
            DatabaseProviderKeys.Sqlite => ComposeSqlite(input, dataDirectory),
            DatabaseProviderKeys.SqlServer => ComposeSqlServer(input),
            DatabaseProviderKeys.PostgreSql => ComposePostgreSql(input),
            _ => Result<string>.Invalid("Choose a supported database."),
        };
    }

    /// <summary>Resolves a SQLite file path: replaces the data directory token and makes relative paths absolute.</summary>
    /// <param name="path">The configured path.</param>
    /// <param name="dataDirectory">The data directory.</param>
    /// <returns>The absolute path.</returns>
    public static string ResolveSqlitePath(string path, string dataDirectory)
    {
        var resolved = path.Trim().Replace(DataDirectoryToken, dataDirectory, StringComparison.OrdinalIgnoreCase);
        return Path.GetFullPath(Path.IsPathRooted(resolved) ? resolved : Path.Combine(dataDirectory, resolved));
    }

    private static Result<string> ValidateRaw(string? raw, string dataDirectory)
    {
        var value = raw?.Trim() ?? string.Empty;
        if (value.Length == 0)
        {
            return Result<string>.Invalid("Enter a connection string.");
        }

        if (value.Length > MaxLength)
        {
            return Result<string>.Invalid($"The connection string must not exceed {MaxLength} characters.");
        }

        value = value.Replace(DataDirectoryToken, dataDirectory, StringComparison.OrdinalIgnoreCase);
        try
        {
            _ = new DbConnectionStringBuilder { ConnectionString = value };
        }
        catch (ArgumentException)
        {
            return Result<string>.Invalid("The connection string is not well-formed (expected key=value pairs separated by ';').");
        }

        return Result<string>.Success(value);
    }

    private static Result<string> ComposeSqlite(SetupConnectionInput input, string dataDirectory)
    {
        if (string.IsNullOrWhiteSpace(input.FilePath))
        {
            return Result<string>.Invalid("Enter the database file path.");
        }

        if (input.FilePath.IndexOfAny(Path.GetInvalidPathChars()) >= 0 || input.FilePath.Contains(';'))
        {
            return Result<string>.Invalid("The database file path contains invalid characters.");
        }

        string path;
        try
        {
            path = ResolveSqlitePath(input.FilePath, dataDirectory);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return Result<string>.Invalid("The database file path is invalid.");
        }

        var builder = new DbConnectionStringBuilder { ["Data Source"] = path };
        return Result<string>.Success(builder.ConnectionString);
    }

    private static Result<string> ComposeSqlServer(SetupConnectionInput input)
    {
        var error = ValidateServerFields(input, requireCredentials: !input.IntegratedSecurity);
        if (error is not null)
        {
            return Result<string>.Invalid(error);
        }

        var server = input.Server.Trim();
        var builder = new DbConnectionStringBuilder
        {
            ["Server"] = input.Port is { } port ? $"{server},{port.ToString(CultureInfo.InvariantCulture)}" : server,
            ["Database"] = input.Database.Trim(),
        };
        if (input.IntegratedSecurity)
        {
            builder["Integrated Security"] = "True";
        }
        else
        {
            builder["User ID"] = input.UserName.Trim();
            builder["Password"] = input.Password;
        }

        builder["Encrypt"] = input.Encrypt ? "True" : "False";
        builder["TrustServerCertificate"] = input.TrustServerCertificate ? "True" : "False";
        builder["Connect Timeout"] = "15";
        builder["Application Name"] = "Servora";
        return Result<string>.Success(builder.ConnectionString);
    }

    private static Result<string> ComposePostgreSql(SetupConnectionInput input)
    {
        var error = ValidateServerFields(input, requireCredentials: true);
        if (error is not null)
        {
            return Result<string>.Invalid(error);
        }

        var sslMode = s_sslModes.FirstOrDefault(m => string.Equals(m, input.SslMode, StringComparison.OrdinalIgnoreCase));
        if (sslMode is null)
        {
            return Result<string>.Invalid("Choose a valid SSL mode.");
        }

        var builder = new DbConnectionStringBuilder
        {
            ["Host"] = input.Server.Trim(),
            ["Port"] = (input.Port ?? 5432).ToString(CultureInfo.InvariantCulture),
            ["Database"] = input.Database.Trim(),
            ["Username"] = input.UserName.Trim(),
            ["Password"] = input.Password,
            ["SSL Mode"] = sslMode,
            ["Timeout"] = "15",
            ["Application Name"] = "Servora",
        };
        return Result<string>.Success(builder.ConnectionString);
    }

    private static string? ValidateServerFields(SetupConnectionInput input, bool requireCredentials)
    {
        if (string.IsNullOrWhiteSpace(input.Server) || input.Server.Length > 253)
        {
            return "Enter the server name.";
        }

        if (input.Port is < 1 or > 65535)
        {
            return "The port must be between 1 and 65535.";
        }

        if (string.IsNullOrWhiteSpace(input.Database) || input.Database.Length > 128)
        {
            return "Enter the database name.";
        }

        if (requireCredentials && string.IsNullOrWhiteSpace(input.UserName))
        {
            return "Enter the user name.";
        }

        return input.Password.Length > 1024 ? "The password is too long." : null;
    }
}
