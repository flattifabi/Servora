namespace Servora.Application.Catalog;

/// <summary>Key figures of the service catalog.</summary>
/// <param name="ActiveItems">Orderable items.</param>
/// <param name="InactiveItems">Inactive items.</param>
/// <param name="Categories">Categories.</param>
/// <param name="ItemsWithApproval">Active items that require approval.</param>
/// <param name="OrdersLast30Days">Requested items created in the last 30 days.</param>
public sealed record CatalogAdminStats(int ActiveItems, int InactiveItems, int Categories, int ItemsWithApproval, int OrdersLast30Days);
