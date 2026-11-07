using Servora.Application.Common;
using Servora.Application.Integration;
using Servora.Application.Integration.DataSources;
using Servora.Application.Security;
using Servora.Domain.Integration;
using Servora.Tests.Unit.Automation;

namespace Servora.Tests.Unit.Integration;

/// <summary>Data source administration: validation, write-only credentials, endpoints, execution log, allow-list and permissions.</summary>
public sealed class DataSourceAdminServiceTests
{
    private static DataSourceModel Model(string baseUrl = "https://api.example.test") => new() { Name = $"API {Guid.NewGuid():N}"[..12], BaseUrl = baseUrl };

    [Fact]
    public async Task Save_RequiresHttpsUnlessAllowed()
    {
        await using var host = await AutomationTestHost.CreateAsync();

        var http = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(Model("http://api.example.test")));
        var withUser = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(Model("https://user:pw@api.example.test")));
        var https = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(Model()));

        Assert.Equal(ResultErrorKind.Validation, http.ErrorKind);
        Assert.False(withUser.Succeeded);
        Assert.True(https.Succeeded, https.Error);
    }

    [Fact]
    public async Task Credentials_AreWriteOnly_AndAudited()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var model = Model();
        model.AuthType = DataSourceAuthType.Bearer;
        var id = (await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(model))).Value;

        var set = await host.WithAsync<IDataSourceAdminService, Result>(s => s.SetCredentialAsync(id, "top-secret-token"));
        var loaded = await host.WithAsync<IDataSourceAdminService, Result<DataSourceModel>>(s => s.GetAsync(id));

        Assert.True(set.Succeeded, set.Error);
        Assert.True(loaded.Value!.CredentialConfigured);
        Assert.Equal("top-secret-token", await host.Secrets.GetAsync(DataSourceSecretNames.BearerToken(id)));
        Assert.DoesNotContain(host.Audit.Entries, e => (e.Message ?? string.Empty).Contains("top-secret", StringComparison.Ordinal));

        Assert.True((await host.WithAsync<IDataSourceAdminService, Result>(s => s.ClearCredentialsAsync(id))).Succeeded);
        Assert.False(await host.Secrets.ExistsAsync(DataSourceSecretNames.BearerToken(id)));
    }

    [Fact]
    public async Task Endpoints_ValidateKeysAndParameters()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var id = (await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(Model()))).Value;

        var good = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveEndpointAsync(new DataSourceEndpointModel
        {
            DataSourceId = id,
            Key = "get_user",
            Name = "Get user",
            PathTemplate = "/users/{user}",
            Parameters = [new EndpointParameterDefinition { Name = "user", Location = EndpointParameterLocation.Path, Required = true }],
        }));
        var badKey = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveEndpointAsync(new DataSourceEndpointModel { DataSourceId = id, Key = "has space", Name = "x" }));
        var absolutePath = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveEndpointAsync(new DataSourceEndpointModel { DataSourceId = id, Key = "evil", Name = "x", PathTemplate = "http://169.254.169.254/" }));

        Assert.True(good.Succeeded, good.Error);
        Assert.False(badKey.Succeeded);
        Assert.False(absolutePath.Succeeded);
        var endpoints = await host.WithAsync<IDataSourceAdminService, Result<IReadOnlyList<DataSourceEndpointModel>>>(s => s.ListEndpointsAsync(id));
        Assert.Single(endpoints.Value!);
    }

    [Fact]
    public async Task Executor_WritesExecutionLog_WithoutParameterValues()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);
        host.Invoker.Body = """{ "ok": true }""";

        var result = await host.WithAsync<IDataSourceExecutor, Result<DataSourceResult>>(s => s.ExecuteAsync(sourceId, "get_user", new Dictionary<string, string> { ["user"] = "private-value-123" }));
        var accessible = await host.WithAsync<IDataSourceExecutor, IReadOnlyList<DataSourceSummary>>(s => s.ListAccessibleAsync());
        var logs = await host.WithAsync<IDataSourceAdminService, Result<PagedResult<DataSourceLogItem>>>(s => s.ListLogsAsync(new PageRequest(), sourceId, false));

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(["get_user", "list_contacts"], accessible.Single().Endpoints.Select(e => e.Key).Order());
        var log = Assert.Single(logs.Value!.Items);
        Assert.True(log.Succeeded);
        Assert.Equal("get_user", log.EndpointKey);
        Assert.DoesNotContain("private-value-123", $"{log.Error}{log.Host}{log.EndpointKey}");
    }

    [Fact]
    public async Task Executor_RejectsUnknownEndpointsAndDisabledSources()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);

        var unknown = await host.WithAsync<IDataSourceExecutor, Result<DataSourceResult>>(s => s.ExecuteAsync(sourceId, "delete_everything", new Dictionary<string, string>()));
        var missingSource = await host.WithAsync<IDataSourceExecutor, Result<DataSourceResult>>(s => s.ExecuteAsync(Guid.CreateVersion7(), "get_user", new Dictionary<string, string>()));

        Assert.False(unknown.Succeeded);
        Assert.False(missingSource.Succeeded);
        Assert.Equal(0, host.Invoker.Calls);
    }

    [Fact]
    public async Task AllowList_ValidatesRanges()
    {
        await using var host = await AutomationTestHost.CreateAsync();

        var valid = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.AddAllowListEntryAsync("10.20.0.0/16", "CMDB API"));
        var invalid = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.AddAllowListEntryAsync("not-a-range", "x"));
        var list = await host.WithAsync<IDataSourceAdminService, Result<IReadOnlyList<NetworkAllowListItem>>>(s => s.ListAllowListAsync());

        Assert.True(valid.Succeeded, valid.Error);
        Assert.False(invalid.Succeeded);
        Assert.Equal("10.20.0.0/16", Assert.Single(list.Value!).Cidr);
    }

    [Fact]
    public async Task Permissions_AreEnforcedServerSide()
    {
        await using var host = await AutomationTestHost.CreateAsync();
        var (sourceId, _) = await IntegrationSeed.AddDataSourceAsync(host);
        host.User.Grant(Permissions.Integration.ReadDataSources);

        var save = await host.WithAsync<IDataSourceAdminService, Result<Guid>>(s => s.SaveAsync(Model()));
        var test = await host.WithAsync<IDataSourceAdminService, Result<DataSourceInvocationResult>>(s => s.TestEndpointAsync(sourceId, "get_user", new Dictionary<string, string> { ["user"] = "a" }));
        var execute = await host.WithAsync<IDataSourceExecutor, Result<DataSourceResult>>(s => s.ExecuteAsync(sourceId, "get_user", new Dictionary<string, string> { ["user"] = "a" }));
        var list = await host.WithAsync<IDataSourceAdminService, Result<PagedResult<DataSourceListItem>>>(s => s.ListAsync(new PageRequest()));

        Assert.Equal(ResultErrorKind.Forbidden, save.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, test.ErrorKind);
        Assert.Equal(ResultErrorKind.Forbidden, execute.ErrorKind);
        Assert.True(list.Succeeded);

        host.User.Grant();
        Assert.Equal(ResultErrorKind.Forbidden, (await host.WithAsync<IDataSourceAdminService, Result<PagedResult<DataSourceListItem>>>(s => s.ListAsync(new PageRequest()))).ErrorKind);
    }
}
