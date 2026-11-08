using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Servora.Web.Api;
using Servora.Web.Security;

namespace Servora.Tests.Unit.Host;

/// <summary>Instance-specific cookie names keep several instances on one host (different ports) apart.</summary>
public sealed class InstanceCookieNamesTests
{
    [Fact]
    public void Names_are_stable_for_the_same_data_directory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "servora-a");
        var first = new InstanceCookieNames(directory);
        var second = new InstanceCookieNames(directory + Path.DirectorySeparatorChar);

        Assert.Equal(first.Authentication, second.Authentication);
        Assert.Matches("^servora\\.auth\\.[0-9a-f]{8}$", first.Authentication);
        Assert.Matches("^servora\\.af\\.[0-9a-f]{8}$", first.Antiforgery);
        Assert.Matches("^servora\\.theme\\.[0-9a-f]{8}$", first.Theme);
    }

    [Fact]
    public void Names_differ_between_data_directories()
    {
        var a = new InstanceCookieNames(Path.Combine(Path.GetTempPath(), "servora-a"));
        var b = new InstanceCookieNames(Path.Combine(Path.GetTempPath(), "servora-b"));

        Assert.NotEqual(a.Authentication, b.Authentication);
        Assert.NotEqual(a.Antiforgery, b.Antiforgery);
    }

    [Fact]
    public void Windows_paths_are_case_insensitive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.Equal(InstanceCookieNames.ComputeSuffix(@"C:\Servora\Data"), InstanceCookieNames.ComputeSuffix(@"c:\servora\data"));
    }

    [Fact]
    public void Api_scheme_selector_uses_the_instance_cookie_name()
    {
        var names = new InstanceCookieNames(Path.Combine(Path.GetTempPath(), "servora-a"));
        var services = new ServiceCollection().AddSingleton(names).BuildServiceProvider();

        var withInstanceCookie = new DefaultHttpContext { RequestServices = services };
        withInstanceCookie.Request.Path = "/api/v1/me";
        withInstanceCookie.Request.Headers.Cookie = $"{names.Authentication}=value";
        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, ApiSchemeSelector.Select(withInstanceCookie));

        // A cookie of another instance (or the pre-T-13 name) does not select the cookie scheme.
        var withForeignCookie = new DefaultHttpContext { RequestServices = services };
        withForeignCookie.Request.Path = "/api/v1/me";
        withForeignCookie.Request.Headers.Cookie = $"{InstanceCookieNames.AuthenticationPrefix}=value";
        Assert.Equal(ApiKeyAuthenticationDefaults.Scheme, ApiSchemeSelector.Select(withForeignCookie));

        // MCP never accepts cookies.
        var mcp = new DefaultHttpContext { RequestServices = services };
        mcp.Request.Path = "/mcp";
        mcp.Request.Headers.Cookie = $"{names.Authentication}=value";
        Assert.Equal(ApiKeyAuthenticationDefaults.Scheme, ApiSchemeSelector.Select(mcp));
    }

    [Theory]
    [InlineData("dark", "dark")]
    [InlineData(" Light ", "light")]
    [InlineData("system", "system")]
    [InlineData("neon", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Saved_theme_is_normalized_before_it_is_handed_to_the_browser(string? stored, string? expected)
    {
        Assert.Equal(expected, UserThemeCookie.Normalize(stored));
    }
}
