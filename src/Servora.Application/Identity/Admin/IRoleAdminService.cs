using Servora.Application.Common;

namespace Servora.Application.Identity.Admin;

/// <summary>
/// Role administration (<c>/admin/roles</c>). Reads require <c>admin.users.read</c>, mutations
/// <c>admin.roles.manage</c>. Adding a permission to a role requires the actor to hold it (escalation guard).
/// System roles cannot be deleted or renamed; the administrator role always keeps <c>*</c>.
/// </summary>
public interface IRoleAdminService
{
    /// <summary>Lists roles (search over name, label and description).</summary>
    /// <param name="search">An optional search term.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The roles (bounded).</returns>
    Task<Result<IReadOnlyList<RoleListItem>>> ListAsync(string? search = null, CancellationToken cancellationToken = default);

    /// <summary>Gets role options for pickers.</summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The options.</returns>
    Task<Result<IReadOnlyList<RoleOption>>> GetOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Gets a role.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The role.</returns>
    Task<Result<RoleDetail>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Creates a role.</summary>
    /// <param name="model">The role data.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The new role identifier.</returns>
    Task<Result<Guid>> CreateAsync(RoleEditModel model, CancellationToken cancellationToken = default);

    /// <summary>Updates a role including its permissions (optimistic concurrency).</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="model">The role data.</param>
    /// <param name="version">The version the edit is based on.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> UpdateAsync(Guid id, RoleEditModel model, long version, CancellationToken cancellationToken = default);

    /// <summary>Deletes a custom role and all of its assignments.</summary>
    /// <param name="id">The role identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result.</returns>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
