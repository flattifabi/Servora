using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Filters of the user list (free-text search, paging and sorting come from <c>PageRequest</c>).</summary>
/// <param name="Status">The status filter.</param>
/// <param name="Source">The account source filter.</param>
/// <param name="RoleId">Only users holding this role directly.</param>
public sealed record UserListQuery(UserStatusFilter Status = UserStatusFilter.All, UserSource? Source = null, Guid? RoleId = null);
