using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>An asset row for lists.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Number">The number (AST…).</param>
/// <param name="DisplayName">The display name.</param>
/// <param name="Category">The category.</param>
/// <param name="Model">The model.</param>
/// <param name="Manufacturer">The manufacturer.</param>
/// <param name="SerialNumber">The serial number.</param>
/// <param name="AssetTag">The asset tag.</param>
/// <param name="State">The lifecycle state.</param>
/// <param name="AssignedToName">The assignee's display name.</param>
/// <param name="Location">The location.</param>
/// <param name="Cost">The purchase cost.</param>
/// <param name="Currency">The currency.</param>
/// <param name="WarrantyEnd">The warranty / license end.</param>
/// <param name="Entitlements">The license entitlements.</param>
/// <param name="Installs">The license installations.</param>
public sealed record AssetSummary(
    Guid Id,
    string Number,
    string DisplayName,
    AssetCategory Category,
    string? Model,
    string? Manufacturer,
    string? SerialNumber,
    string? AssetTag,
    AssetState State,
    string? AssignedToName,
    string? Location,
    decimal Cost,
    string Currency,
    DateOnly? WarrantyEnd,
    int? Entitlements,
    int? Installs);
