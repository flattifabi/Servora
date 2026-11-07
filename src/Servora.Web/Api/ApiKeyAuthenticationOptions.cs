using Microsoft.AspNetCore.Authentication;

namespace Servora.Web.Api;

/// <summary>Options of the <c>ApiKey</c> authentication scheme.</summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Gets or sets the realm reported in the <c>WWW-Authenticate</c> header.</summary>
    public string Realm { get; set; } = "servora";
}
