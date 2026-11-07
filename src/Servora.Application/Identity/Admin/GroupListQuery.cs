using Servora.Domain.Identity;

namespace Servora.Application.Identity.Admin;

/// <summary>Filters of the group list (search, paging and sorting come from <c>PageRequest</c>).</summary>
/// <param name="Type">Only groups of this type.</param>
/// <param name="ActiveOnly">Only active groups when <see langword="true"/>, only inactive when <see langword="false"/>, all when <see langword="null"/>.</param>
public sealed record GroupListQuery(GroupType? Type = null, bool? ActiveOnly = null);
