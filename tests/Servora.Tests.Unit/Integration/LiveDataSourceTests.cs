using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Domain.Integration;
using Servora.Infrastructure.Integration;
using Servora.Tests.Unit.Automation.Fakes;

namespace Servora.Tests.Unit.Integration;

/// <summary>
/// Opt-in live check of the production HTTP pipeline (system DNS, SSRF connect callback, TLS validation) against a public
/// HTTPS API. Runs only when <c>SERVORA_NETWORK_TESTS=1</c> so CI and offline builds stay deterministic.
/// </summary>
public sealed class LiveDataSourceTests
{
    private static bool Enabled => Environment.GetEnvironmentVariable("SERVORA_NETWORK_TESTS") == "1";

    private static HttpDataSourceInvoker CreateInvoker()
    {
        var guard = new SsrfGuard(new SystemDnsResolver(), new StaticAllowListProvider());
        var handler = DataSourceHttp.CreateHandler(guard);
        return new HttpDataSourceInvoker(new StubHttpClientFactory(handler), new InMemorySecretStore(), new MemoryCache(new MemoryCacheOptions()), guard, NullLogger<HttpDataSourceInvoker>.Instance);
    }

    [Fact]
    public async Task PublicHttpsApi_IsExecuted()
    {
        if (!Enabled)
        {
            return;
        }

        var source = new DataSource { Name = "GitHub", BaseUrl = "https://api.github.com", TimeoutSeconds = 20, DefaultHeadersJson = """{ "Accept": "application/vnd.github+json" }""" };
        var endpoint = new DataSourceEndpoint { Key = "repo", Name = "Repository", PathTemplate = "/repos/dotnet/runtime", ResponsePath = "full_name", ParametersJson = "[]" };

        var result = await CreateInvoker().InvokeAsync(source, endpoint, new Dictionary<string, string>());

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(200, result.Value!.StatusCode);
        Assert.Equal("\"dotnet/runtime\"", result.Value.MappedBody);
    }

    [Theory]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://169.254.169.254")]
    [InlineData("http://localhost")]
    public async Task InternalTargets_AreBlockedWithRealDns(string baseUrl)
    {
        if (!Enabled)
        {
            return;
        }

        var source = new DataSource { Name = "Internal", BaseUrl = baseUrl, AllowHttp = true };
        var endpoint = new DataSourceEndpoint { Key = "x", Name = "x", PathTemplate = "/latest/meta-data/", ParametersJson = "[]" };

        var result = await CreateInvoker().InvokeAsync(source, endpoint, new Dictionary<string, string>());

        Assert.False(result.Succeeded);
        Assert.Contains("SSRF", result.Error);
    }
}
