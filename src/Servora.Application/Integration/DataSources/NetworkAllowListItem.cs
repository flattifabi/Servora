namespace Servora.Application.Integration.DataSources;

/// <summary>A network allow-list entry.</summary>
/// <param name="Id">The entry.</param>
/// <param name="Cidr">The CIDR range.</param>
/// <param name="Description">The justification.</param>
/// <param name="IsEnabled">Whether the entry is in effect.</param>
/// <param name="CreatedAt">The creation time.</param>
/// <param name="CreatedBy">The creator.</param>
public sealed record NetworkAllowListItem(Guid Id, string Cidr, string? Description, bool IsEnabled, DateTimeOffset CreatedAt, string? CreatedBy);
