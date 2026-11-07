using Servora.Application.Common;

namespace Servora.Application.Identity.Admin;

/// <summary>
/// User administration (<c>/admin/users</c>). Reads require <c>admin.users.read</c>, mutations
/// <c>admin.users.manage</c>. Accounts holding permissions the actor lacks cannot be managed (escalation guard), and
/// roles can only be granted when the actor holds all of their permissions. Every change is audited.
/// </summary>
public interface IUserAdminService
{
    /// <summary>Lists users with filters, search, sorting and paging.</summary>
    /// <param name="query">The filters.</param>
    /// <param name="page">Paging, sorting and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<UserListItem>>> ListAsync(UserListQuery query, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets KPI figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<UserStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a user.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The user.</returns>
    Task<Result<UserDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a local or service account.</summary>
    /// <param name="model">The user data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new user identifier.</returns>
    Task<Result<Guid>> CreateAsync(UserEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates profile fields of a user (optimistic concurrency).</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="model">The user data.</param>
    /// <param name="version">The version the edit is based on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, UserEditModel model, long version, CancellationToken cancellationToken = default);

    /// <summary>Activates or deactivates a user. Deactivation signs the user out everywhere.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="active">The new state.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken = default);

    /// <summary>Sets a new password for a local account and requires a change at next sign-in.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="newPassword">The temporary password (validated against the policy).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ResetPasswordAsync(Guid id, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Clears a lockout.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UnlockAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Signs the user out of every session by rotating the security stamp.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> ForceSignOutAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Deletes a user. Prefer deactivation for users referenced by records.</summary>
    /// <param name="id">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Assigns a role directly to a user (escalation guard applies).</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AddRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Removes a directly assigned role from a user.</summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveRoleAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}
