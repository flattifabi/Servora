using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Servora.Domain.Identity;
using Servora.Infrastructure.Security;

namespace Servora.Web.Security;

/// <summary>Builds the cookie principal for an authenticated <see cref="User"/>.</summary>
public static class UserPrincipalFactory
{
    /// <summary>Creates the principal.</summary>
    /// <param name="user">The user.</param>
    /// <param name="authSource">The authentication source: <c>local</c>, <c>directory</c> or <c>windows</c>.</param>
    /// <returns>The claims principal for the cookie scheme.</returns>
    public static ClaimsPrincipal Create(User user, string authSource)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ServoraClaimTypes.DisplayName, user.DisplayName),
            new(ServoraClaimTypes.SecurityStamp, user.SecurityStamp),
            new(ServoraClaimTypes.AuthSource, authSource),
            new(ServoraClaimTypes.ActorType, "user"),
        };

        if (!string.IsNullOrEmpty(user.Email))
        {
            claims.Add(new Claim(ClaimTypes.Email, user.Email));
        }

        if (user.MustChangePassword && user.Source == UserSource.Local)
        {
            claims.Add(new Claim(ServoraClaimTypes.MustChangePassword, "true"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
