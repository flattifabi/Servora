using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Servora.Infrastructure.Security;
using Servora.Web.Api;
using Servora.Web.Security;

namespace Servora.Tests.Unit.Host;

/// <summary>
/// API paths must answer with JSON problem details (never HTML sign-in redirects): forced password change, cookie
/// challenges / forbids and empty error responses.
/// </summary>
public sealed class ApiProblemResponseTests
{
    [Theory]
    [InlineData("/api/v1/me")]
    [InlineData("/mcp")]
    [InlineData("/openapi/v1.json")]
    public async Task Must_change_password_session_gets_json_403_on_api_paths(string path)
    {
        var context = CreateContext(path, mustChangePassword: true);
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.Equal("Password change required", problem.GetProperty("title").GetString());
        Assert.Equal(403, problem.GetProperty("status").GetInt32());
    }

    [Fact]
    public async Task Must_change_password_session_is_redirected_on_ui_paths()
    {
        var context = CreateContext("/incidents", mustChangePassword: true);
        var middleware = new MustChangePasswordMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal(MustChangePasswordMiddleware.ChangePasswordPath, context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Regular_session_passes_through()
    {
        var context = CreateContext("/api/v1/me", mustChangePassword: false);
        var nextCalled = false;
        var middleware = new MustChangePasswordMiddleware(_ => { nextCalled = true; return Task.CompletedTask; });

        await middleware.InvokeAsync(context);

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task Cookie_challenge_on_api_path_returns_json_401_instead_of_redirect()
    {
        var context = CreateContext("/api/v1/table/incident", mustChangePassword: false, method: HttpMethods.Post);
        var redirect = CreateRedirectContext(context, "/login?ReturnUrl=%2Fapi%2Fv1%2Ftable%2Fincident");

        await ApiAwareCookieRedirects.RedirectToLoginAsync(redirect);

        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
        Assert.True(string.IsNullOrEmpty(context.Response.Headers.Location.ToString()));
        Assert.StartsWith(ApiProblemWriter.ProblemContentType, context.Response.ContentType);
        var problem = await ReadProblemAsync(context);
        Assert.Equal("Unauthorized", problem.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cookie_forbid_on_api_path_returns_json_403()
    {
        var context = CreateContext("/api/v1/me", mustChangePassword: false);
        var redirect = CreateRedirectContext(context, "/access-denied");

        await ApiAwareCookieRedirects.RedirectToAccessDeniedAsync(redirect);

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
        Assert.Equal("Forbidden", (await ReadProblemAsync(context)).GetProperty("title").GetString());
    }

    [Fact]
    public async Task Cookie_challenge_on_ui_path_redirects_to_login()
    {
        var context = CreateContext("/incidents", mustChangePassword: false);
        var redirect = CreateRedirectContext(context, "/login?ReturnUrl=%2Fincidents");

        await ApiAwareCookieRedirects.RedirectToLoginAsync(redirect);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/login?ReturnUrl=%2Fincidents", context.Response.Headers.Location.ToString());
    }

    [Theory]
    [InlineData(StatusCodes.Status404NotFound, "Not found")]
    [InlineData(StatusCodes.Status405MethodNotAllowed, "Method not allowed")]
    [InlineData(StatusCodes.Status503ServiceUnavailable, "Service unavailable")]
    public async Task Empty_api_error_response_gets_problem_body(int status, string title)
    {
        var context = CreateContext("/api/v1/does-not-exist", mustChangePassword: false);
        context.Response.StatusCode = status;

        await ApiProblemWriter.WriteStatusCodeAsync(new StatusCodeContext(context, new StatusCodePagesOptions(), _ => Task.CompletedTask));

        Assert.Equal(status, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.Equal(title, problem.GetProperty("title").GetString());
        Assert.Equal(status, problem.GetProperty("status").GetInt32());
    }

    [Theory]
    [InlineData("/api/v1/me", true)]
    [InlineData("/mcp", true)]
    [InlineData("/openapi/v1.json", true)]
    [InlineData("/apis", false)]
    [InlineData("/incidents", false)]
    [InlineData("/", false)]
    public void Detects_api_requests(string path, bool expected)
    {
        Assert.Equal(expected, ApiProblemWriter.IsApiRequest(CreateContext(path, mustChangePassword: false)));
    }

    private static DefaultHttpContext CreateContext(string path, bool mustChangePassword, string method = "GET")
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        var context = new DefaultHttpContext { RequestServices = services.BuildServiceProvider() };
        context.Request.Path = path;
        context.Request.Method = method;
        context.Request.Headers.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9";
        context.Response.Body = new MemoryStream();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new(ClaimTypes.Name, "admin") };
        if (mustChangePassword)
        {
            claims.Add(new Claim(ServoraClaimTypes.MustChangePassword, "true"));
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return context;
    }

    private static RedirectContext<CookieAuthenticationOptions> CreateRedirectContext(HttpContext context, string redirectUri) =>
        new(context, new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler)), new CookieAuthenticationOptions(), new AuthenticationProperties(), redirectUri);

    private static async Task<JsonElement> ReadProblemAsync(HttpContext context)
    {
        Assert.StartsWith(ApiProblemWriter.ProblemContentType, context.Response.ContentType);
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(context.Response.Body);
        return document.RootElement.Clone();
    }
}
