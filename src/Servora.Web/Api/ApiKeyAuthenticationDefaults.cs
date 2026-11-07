namespace Servora.Web.Api;

/// <summary>Names used by the API key authentication.</summary>
public static class ApiKeyAuthenticationDefaults
{
    /// <summary>The API key authentication scheme.</summary>
    public const string Scheme = "ApiKey";

    /// <summary>The policy scheme that forwards <c>/api</c>, <c>/mcp</c> and <c>/openapi</c> requests to the API key scheme.</summary>
    public const string SelectorScheme = "Servora";

    /// <summary>The API key header.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <summary>The authorization policy of the MCP endpoint (API key scheme only + <c>mcp.access</c>).</summary>
    public const string McpPolicy = "servora:mcp";

    /// <summary>The <c>HttpContext.Items</c> key holding the authentication failure reason.</summary>
    public const string FailureItemKey = "servora:apikey-failure";

    /// <summary>The cookie name of the interactive authentication (see Program.cs).</summary>
    public const string AuthCookieName = "servora.auth";
}
