using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Domain.Identity;

namespace Servora.Web.Security;

/// <summary>
/// Applies the theme saved in the user's profile (<see cref="User.Theme"/>) after every interactive sign-in: the cookie
/// <c>OnSignedIn</c> event writes a short-lived, script-readable cookie that the document head script (App.razor) copies
/// into <c>localStorage</c> and deletes before the first paint. The theme toggle keeps working per browser afterwards.
/// </summary>
public static class UserThemeCookie
{
    /// <summary>The lifetime of the hand-over cookie.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private static readonly string[] s_themes = ["dark", "light", "system"];

    /// <summary>Normalizes a stored theme value.</summary>
    /// <param name="theme">The stored value.</param>
    /// <returns><c>dark</c>, <c>light</c> or <c>system</c>; <see langword="null"/> for empty or unknown values.</returns>
    public static string? Normalize(string? theme)
    {
        var value = theme?.Trim().ToLowerInvariant();
        return value is not null && s_themes.Contains(value) ? value : null;
    }

    /// <summary>Cookie authentication <c>OnSignedIn</c> handler.</summary>
    /// <param name="context">The signed-in context.</param>
    /// <returns>A task that completes when the cookie was appended.</returns>
    public static async Task OnSignedInAsync(CookieSignedInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!Guid.TryParse(context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        string? theme;
        try
        {
            var dbFactory = context.HttpContext.RequestServices.GetRequiredService<IAppDbContextFactory>();
            await using var db = await dbFactory.CreateDbContextAsync(context.HttpContext.RequestAborted);
            theme = await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => u.Theme).FirstOrDefaultAsync(context.HttpContext.RequestAborted);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The theme is cosmetic: never let it break the sign-in.
            context.HttpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger(typeof(UserThemeCookie)).LogDebug(ex, "The saved theme could not be loaded.");
            return;
        }

        if (Normalize(theme) is not { } value)
        {
            return;
        }

        var names = context.HttpContext.RequestServices.GetRequiredService<InstanceCookieNames>();
        context.Response.Cookies.Append(names.Theme, value, new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
            Secure = context.Request.IsHttps,
            MaxAge = Lifetime,
        });
    }
}
