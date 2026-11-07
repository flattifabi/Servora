using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Servora.Application.Common;
using Servora.Domain.Identity;
using Servora.Infrastructure.Security;

namespace Servora.Web.Security;

/// <summary>
/// Checks that the user behind a cookie or circuit still exists, is active and that the security stamp has not
/// changed (password change, deactivation, forced sign-out). Results are cached for 30 seconds per user.
/// </summary>
/// <param name="dbFactory">The database context factory.</param>
/// <param name="cache">The memory cache.</param>
public sealed class SecurityStampValidator(IAppDbContextFactory dbFactory, IMemoryCache cache)
{
    private static readonly TimeSpan s_cacheDuration = TimeSpan.FromSeconds(30);

    /// <summary>Validates the principal.</summary>
    /// <param name="principal">The principal.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns><see langword="true"/> when the principal is still valid.</returns>
    public async Task<bool> IsValidAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (principal.FindFirstValue(ServoraClaimTypes.ActorType) is "apikey" or "agent")
        {
            return true;
        }

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return false;
        }

        var stamp = principal.FindFirstValue(ServoraClaimTypes.SecurityStamp);
        var key = $"stamp:{userId:N}";
        if (!cache.TryGetValue(key, out (bool Active, string Stamp) state))
        {
            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var row = await db.Set<User>().AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.IsActive, u.SecurityStamp }).FirstOrDefaultAsync(cancellationToken);
            state = row is null ? (false, string.Empty) : (row.IsActive, row.SecurityStamp);
            cache.Set(key, state, s_cacheDuration);
        }

        return state.Active && string.Equals(state.Stamp, stamp, StringComparison.Ordinal);
    }

    /// <summary>Removes the cached state of a user (call after changing the security stamp).</summary>
    /// <param name="userId">The user identifier.</param>
    public void Invalidate(Guid userId) => cache.Remove($"stamp:{userId:N}");
}
