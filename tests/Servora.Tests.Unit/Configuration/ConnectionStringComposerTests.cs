using System.Data.Common;
using Servora.Application.Configuration;

namespace Servora.Tests.Unit.Configuration;

/// <summary>Tests for the setup wizard's connection string builder.</summary>
public sealed class ConnectionStringComposerTests
{
    private static readonly string s_dataDirectory = Path.Combine(Path.GetTempPath(), "servora-data");

    [Fact]
    public void Sqlite_RelativePathIsResolvedInsideDataDirectory()
    {
        var result = ConnectionStringComposer.Compose(new SetupConnectionInput { Provider = DatabaseProviderKeys.Sqlite, FilePath = "servora.db" }, s_dataDirectory);

        Assert.True(result.Succeeded);
        var builder = new DbConnectionStringBuilder { ConnectionString = result.Value };
        Assert.Equal(Path.Combine(s_dataDirectory, "servora.db"), builder["Data Source"]);
    }

    [Fact]
    public void Sqlite_RejectsSeparatorInjection() =>
        Assert.False(ConnectionStringComposer.Compose(new SetupConnectionInput { Provider = DatabaseProviderKeys.Sqlite, FilePath = "a.db;Password=x" }, s_dataDirectory).Succeeded);

    [Fact]
    public void SqlServer_EscapesPasswordsWithSeparators()
    {
        var input = new SetupConnectionInput
        {
            Provider = DatabaseProviderKeys.SqlServer,
            Server = @"sql01\SERVORA",
            Database = "Servora",
            UserName = "servora",
            Password = "p;a=ss'\"word",
            IntegratedSecurity = false,
        };

        var result = ConnectionStringComposer.Compose(input, s_dataDirectory);

        Assert.True(result.Succeeded, result.Error);
        var builder = new DbConnectionStringBuilder { ConnectionString = result.Value };
        Assert.Equal("p;a=ss'\"word", builder["Password"]);
        Assert.Equal(@"sql01\SERVORA", builder["Server"]);
        Assert.Equal("True", builder["Encrypt"]);
        Assert.Equal("False", builder["TrustServerCertificate"]);
    }

    [Fact]
    public void SqlServer_IntegratedSecurity_OmitsCredentials()
    {
        var input = new SetupConnectionInput { Provider = DatabaseProviderKeys.SqlServer, Server = "localhost", Port = 1433, Database = "Servora", IntegratedSecurity = true };

        var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionStringComposer.Compose(input, s_dataDirectory).Value };

        Assert.Equal("True", builder["Integrated Security"]);
        Assert.Equal("localhost,1433", builder["Server"]);
        Assert.False(builder.ContainsKey("Password"));
    }

    [Fact]
    public void PostgreSql_ValidatesSslModeAndRequiresUser()
    {
        var input = new SetupConnectionInput { Provider = DatabaseProviderKeys.PostgreSql, Server = "db", Database = "servora", UserName = "servora", SslMode = "require" };
        var builder = new DbConnectionStringBuilder { ConnectionString = ConnectionStringComposer.Compose(input, s_dataDirectory).Value };
        Assert.Equal("Require", builder["SSL Mode"]);
        Assert.Equal("5432", builder["Port"]);

        input.SslMode = "Sometimes";
        Assert.False(ConnectionStringComposer.Compose(input, s_dataDirectory).Succeeded);

        input.SslMode = "Prefer";
        input.UserName = string.Empty;
        Assert.False(ConnectionStringComposer.Compose(input, s_dataDirectory).Succeeded);
    }

    [Fact]
    public void Raw_ReplacesDataDirectoryToken_AndRejectsMalformedStrings()
    {
        var raw = ConnectionStringComposer.Compose(new SetupConnectionInput { UseRawConnectionString = true, RawConnectionString = "Data Source={DataDirectory}/x.db" }, s_dataDirectory);
        Assert.Equal($"Data Source={s_dataDirectory}/x.db", raw.Value);

        Assert.False(ConnectionStringComposer.Compose(new SetupConnectionInput { UseRawConnectionString = true, RawConnectionString = "Server='unterminated" }, s_dataDirectory).Succeeded);
        Assert.False(ConnectionStringComposer.Compose(new SetupConnectionInput { UseRawConnectionString = true, RawConnectionString = " " }, s_dataDirectory).Succeeded);
    }

    [Fact]
    public void UnknownProvider_IsRejected() =>
        Assert.False(ConnectionStringComposer.Compose(new SetupConnectionInput { Provider = "Oracle" }, s_dataDirectory).Succeeded);
}
