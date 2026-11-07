using Servora.Application.Common;

namespace Servora.Application.Identity.Admin;

/// <summary>
/// Group administration (<c>/admin/groups</c>). Reads require <c>admin.users.read</c>, mutations
/// <c>admin.groups.manage</c>. Granting a role to a group, or adding a member to a group that grants roles, requires
/// the actor to hold every permission handed out (escalation guard). Every change is audited and invalidates the
/// permission cache of the affected members.
/// </summary>
public interface IGroupAdminService
{
    /// <summary>Lists groups.</summary>
    /// <param name="query">The filters.</param>
    /// <param name="page">Paging, sorting and search.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The page.</returns>
    Task<Result<PagedResult<GroupListItem>>> ListAsync(GroupListQuery query, PageRequest page, CancellationToken cancellationToken = default);

    /// <summary>Gets KPI figures.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The figures.</returns>
    Task<Result<GroupStats>> GetStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a group with members and roles.</summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The group.</returns>
    Task<Result<GroupDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a group.</summary>
    /// <param name="model">The group data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new group identifier.</returns>
    Task<Result<Guid>> CreateAsync(GroupEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates a group (optimistic concurrency).</summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="model">The group data.</param>
    /// <param name="version">The version the edit is based on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, GroupEditModel model, long version, CancellationToken cancellationToken = default);

    /// <summary>Deletes a group with its memberships and role grants.</summary>
    /// <param name="id">The group identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds members to a group.</summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="userIds">The users to add (max. 200 per call).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The number of members added.</returns>
    Task<Result<int>> AddMembersAsync(Guid groupId, IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);

    /// <summary>Removes a member from a group.</summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveMemberAsync(Guid groupId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Grants a role to all members of a group.</summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> AddRoleAsync(Guid groupId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Revokes a role from a group.</summary>
    /// <param name="groupId">The group identifier.</param>
    /// <param name="roleId">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> RemoveRoleAsync(Guid groupId, Guid roleId, CancellationToken cancellationToken = default);
}
