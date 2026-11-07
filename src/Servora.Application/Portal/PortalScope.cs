using Microsoft.EntityFrameworkCore;
using Servora.Application.Common;
using Servora.Application.Security;
using Servora.Domain.Catalog;
using Servora.Domain.Itsm;

namespace Servora.Application.Portal;

/// <summary>
/// The security boundary of the portal: who may use it and which records belong to the current user. Every portal
/// query starts from these helpers so that data access is always restricted server-side to the user's own records.
/// </summary>
internal static class PortalScope
{
    /// <summary>Gets the current user's identifier when the actor is a user holding <c>portal.access</c>.</summary>
    /// <param name="currentUser">The current actor.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user identifier, or <see langword="null"/> when the portal must not be used.</returns>
    public static async Task<Guid?> PortalUserAsync(ICurrentUser currentUser, CancellationToken cancellationToken) =>
        currentUser.UserId is { } userId && await currentUser.HasPermissionAsync(Permissions.Portal.Access, cancellationToken) ? userId : null;

    /// <summary>Incidents the user is the caller of or opened (read only).</summary>
    /// <param name="db">The context.</param>
    /// <param name="userId">The user.</param>
    /// <returns>The query.</returns>
    public static IQueryable<Incident> OwnIncidents(IAppDbContext db, Guid userId) =>
        db.Set<Incident>().AsNoTracking().Where(i => i.CallerId == userId || i.OpenedById == userId);

    /// <summary>Requests the user receives or ordered (read only).</summary>
    /// <param name="db">The context.</param>
    /// <param name="userId">The user.</param>
    /// <returns>The query.</returns>
    public static IQueryable<Request> OwnRequests(IAppDbContext db, Guid userId) =>
        db.Set<Request>().AsNoTracking().Where(r => r.RequestedForId == userId || r.OpenedById == userId);

    /// <summary>Checks whether an incident belongs to the user.</summary>
    /// <param name="incident">The incident.</param>
    /// <param name="userId">The user.</param>
    /// <returns><see langword="true"/> when the user is the caller or opened the incident.</returns>
    public static bool Owns(Incident incident, Guid userId) => incident.CallerId == userId || incident.OpenedById == userId;

    /// <summary>Gets whether a request state is open.</summary>
    /// <param name="state">The state.</param>
    /// <returns><see langword="true"/> for submitted and in-progress requests.</returns>
    public static bool IsOpen(RequestState state) => state is RequestState.Submitted or RequestState.InProgress;

    /// <summary>Normalizes a search term (trimmed, lower case, max. 100 characters).</summary>
    /// <param name="search">The raw term.</param>
    /// <returns>The term or <see langword="null"/> when empty.</returns>
    public static string? Term(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var term = search.Trim().ToLowerInvariant();
        return term.Length > 100 ? term[..100] : term;
    }
}
