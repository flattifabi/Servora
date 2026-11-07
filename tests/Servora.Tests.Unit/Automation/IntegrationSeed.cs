using Servora.Domain.Integration;

namespace Servora.Tests.Unit.Automation;

/// <summary>Seeds data sources for automation and integration tests.</summary>
internal static class IntegrationSeed
{
    /// <summary>Adds an enabled HTTPS data source with a <c>get_user</c> (path parameter) and a <c>list_contacts</c> endpoint.</summary>
    /// <param name="host">The test host.</param>
    /// <returns>The data source and the <c>get_user</c> endpoint identifiers.</returns>
    public static async Task<(Guid SourceId, Guid EndpointId)> AddDataSourceAsync(AutomationTestHost host)
    {
        var source = new DataSource { Name = $"API {Guid.NewGuid():N}"[..12], BaseUrl = "https://api.example.test", RateLimitPerMinute = 1000 };
        var endpoint = new DataSourceEndpoint
        {
            DataSourceId = source.Id,
            Key = "get_user",
            Name = "Get user",
            Method = "GET",
            PathTemplate = "/users/{user}",
            ParametersJson = """[ { "name": "user", "location": "Path", "type": "String", "required": true, "pattern": "^[A-Za-z0-9-]+$" } ]""",
        };
        var list = new DataSourceEndpoint
        {
            DataSourceId = source.Id,
            Key = "list_contacts",
            Name = "List contacts",
            Method = "GET",
            PathTemplate = "/contacts",
            ParametersJson = "[]",
        };
        await using var db = host.Create();
        db.Set<DataSource>().Add(source);
        db.Set<DataSourceEndpoint>().AddRange(endpoint, list);
        await db.SaveChangesAsync();
        return (source.Id, endpoint.Id);
    }
}
