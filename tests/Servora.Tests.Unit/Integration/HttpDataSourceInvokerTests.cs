using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Servora.Application.Integration.DataSources;
using Servora.Domain.Integration;
using Servora.Infrastructure.Integration;
using Servora.Tests.Unit.Automation.Fakes;

namespace Servora.Tests.Unit.Integration;

/// <summary>
/// Tests of <see cref="HttpDataSourceInvoker"/> with a stub HTTP handler: request building, response mapping,
/// authentication, retries, size limits and SSRF blocking of base URLs and redirects.
/// </summary>
public sealed class HttpDataSourceInvokerTests
{
    private readonly FakeDnsResolver _dns = new();
    private readonly InMemorySecretStore _secrets = new();

    public HttpDataSourceInvokerTests()
    {
        _dns.Hosts["api.example.com"] = [IPAddress.Parse("93.184.216.34")];
        _dns.Hosts["other.example.com"] = [IPAddress.Parse("93.184.216.35")];
    }

    private static DataSource Source(string baseUrl, DataSourceAuthType auth = DataSourceAuthType.None) => new()
    {
        Id = Guid.CreateVersion7(),
        Name = "Test API",
        BaseUrl = baseUrl,
        AuthType = auth,
        MaxRetries = 2,
        TimeoutSeconds = 10,
    };

    private static DataSourceEndpoint Endpoint(string path = "/users/{id}", string? responsePath = null) => new()
    {
        Key = "get_user",
        Name = "Get user",
        Method = "GET",
        PathTemplate = path,
        ResponsePath = responsePath,
        ParametersJson = """
            [
              { "name": "id", "location": "Path", "type": "Integer", "required": true },
              { "name": "q", "location": "Query", "type": "String" }
            ]
            """,
    };

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Redirect(string location) =>
        new(HttpStatusCode.Found) { Headers = { Location = new Uri(location) } };

    private (HttpDataSourceInvoker Invoker, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpMessageHandler(respond);
        var invoker = new HttpDataSourceInvoker(
            new StubHttpClientFactory(handler),
            _secrets,
            new MemoryCache(new MemoryCacheOptions()),
            new SsrfGuard(_dns, new StaticAllowListProvider()),
            NullLogger<HttpDataSourceInvoker>.Instance)
        {
            RetryBaseDelay = TimeSpan.Zero,
        };
        return (invoker, handler);
    }

    [Fact]
    public async Task ExecutesHttpsRequest_AndMapsResponsePath()
    {
        var (invoker, handler) = Create(_ => Json("""{ "data": { "items": [ { "id": 42, "name": "Ada" } ] } }"""));

        var result = await invoker.InvokeAsync(Source("https://api.example.com/v1"), Endpoint(responsePath: "data.items"), new Dictionary<string, string> { ["id"] = "42", ["q"] = "a b&c" });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(200, result.Value!.StatusCode);
        Assert.Contains("Ada", result.Value.MappedBody);
        Assert.StartsWith("[", result.Value.MappedBody);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.example.com/v1/users/42?q=a%20b%26c", request.RequestUri!.AbsoluteUri);
        Assert.Equal("api.example.com", result.Value.Host);
    }

    [Theory]
    [InlineData("http://127.0.0.1:8080")]
    [InlineData("https://127.0.0.1")]
    [InlineData("https://169.254.169.254")]
    [InlineData("https://[::1]")]
    [InlineData("https://10.0.0.8")]
    public async Task BlocksInternalBaseUrls_WithoutSendingRequests(string baseUrl)
    {
        var (invoker, handler) = Create(_ => Json("{}"));
        var source = Source(baseUrl);
        source.AllowHttp = true;

        var result = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.False(result.Succeeded);
        Assert.Contains("SSRF", result.Error);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("https://169.254.169.254/latest/meta-data/iam/", "Redirect blocked")]
    [InlineData("https://127.0.0.1/admin", "Redirect blocked")]
    [InlineData("http://169.254.169.254/latest/meta-data/iam/", "insecure URL was blocked")]
    public async Task BlocksRedirectsToInternalOrInsecureTargets(string location, string expected)
    {
        var (invoker, handler) = Create(_ => Redirect(location));

        var result = await invoker.InvokeAsync(Source("https://api.example.com"), Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.False(result.Succeeded);
        Assert.Contains(expected, result.Error);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task FollowsAtMostThreeValidatedRedirects()
    {
        var (invoker, handler) = Create(_ => Redirect($"https://other.example.com/hop/{Guid.NewGuid():N}"));

        var result = await invoker.InvokeAsync(Source("https://api.example.com"), Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.False(result.Succeeded);
        Assert.Contains("Too many redirects", result.Error);
        Assert.Equal(1 + HttpDataSourceInvoker.MaxRedirects, handler.Requests.Count);
    }

    [Fact]
    public async Task FollowsRedirectToPublicHost()
    {
        var (invoker, handler) = Create(request => request.RequestUri!.Host == "api.example.com" ? Redirect("https://other.example.com/moved") : Json("""{ "ok": true }"""));

        var result = await invoker.InvokeAsync(Source("https://api.example.com"), Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal("other.example.com", result.Value!.Host);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task RejectsUnknownAndInvalidParameters()
    {
        var (invoker, handler) = Create(_ => Json("{}"));
        var source = Source("https://api.example.com");

        var unknown = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "1", ["url"] = "http://evil" });
        var notInteger = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "../admin" });
        var missing = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string>());

        Assert.Contains("not allowed", unknown.Error);
        Assert.False(notInteger.Succeeded);
        Assert.Contains("required", missing.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AddsBearerTokenFromSecretStore_WithoutLeakingIt()
    {
        var (invoker, handler) = Create(_ => Json("{}"));
        var source = Source("https://api.example.com", DataSourceAuthType.Bearer);

        var withoutSecret = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "1" });
        await _secrets.SetAsync(DataSourceSecretNames.BearerToken(source.Id), "s3cr3t-token");
        var withSecret = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.Contains("not configured", withoutSecret.Error);
        Assert.True(withSecret.Succeeded, withSecret.Error);
        Assert.Equal("Bearer s3cr3t-token", Assert.Single(handler.Requests).Headers.Authorization!.ToString());
        Assert.DoesNotContain("s3cr3t", string.Join(";", withSecret.Value!.Headers.Values) + withSecret.Value.Body);
    }

    [Fact]
    public async Task RetriesTransientFailures()
    {
        var calls = 0;
        var (invoker, handler) = Create(_ => ++calls < 3 ? Json("{}", HttpStatusCode.ServiceUnavailable) : Json("""{ "ok": 1 }"""));

        var result = await invoker.InvokeAsync(Source("https://api.example.com"), Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(3, result.Value!.Attempts);
        Assert.Equal(3, handler.Requests.Count);
    }

    [Fact]
    public async Task TruncatesResponsesAtSizeLimit()
    {
        var (invoker, _) = Create(_ => Json("\"" + new string('x', 5000) + "\""));
        var source = Source("https://api.example.com");
        source.MaxResponseBytes = 1024;

        var result = await invoker.InvokeAsync(source, Endpoint(), new Dictionary<string, string> { ["id"] = "1" });

        Assert.True(result.Succeeded, result.Error);
        Assert.True(result.Value!.Truncated);
        Assert.True(result.Value.Body.Length <= 1024);
    }

    [Theory]
    [InlineData("""{ "a": { "b": [ 1, 2, 3 ] } }""", "a.b", "[1,2,3]")]
    [InlineData("""{ "a": { "b": [ { "c": "x" } ] } }""", "a.b[0].c", "\"x\"")]
    [InlineData("""{ "a": 1 }""", "missing", null)]
    [InlineData("not json", "a", null)]
    public void MapResponse_SelectsJsonPath(string body, string path, string? expected)
    {
        var mapped = HttpDataSourceInvoker.MapResponse(body, path);

        Assert.Equal(expected, mapped?.Replace(" ", string.Empty, StringComparison.Ordinal).Replace("\n", string.Empty, StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal));
    }
}
