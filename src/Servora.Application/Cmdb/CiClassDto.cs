using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>A CI class with its own attribute definitions and usage count.</summary>
/// <param name="Id">The identifier.</param>
/// <param name="Key">The technical key.</param>
/// <param name="Label">The label.</param>
/// <param name="Icon">The icon name.</param>
/// <param name="Color">The accent color.</param>
/// <param name="ParentKey">The parent class key.</param>
/// <param name="Description">The description.</param>
/// <param name="IsSystem">Whether the class ships with the product.</param>
/// <param name="SortOrder">The display order.</param>
/// <param name="Attributes">The attributes defined on this class (without inherited ones).</param>
/// <param name="CiCount">The number of CIs of exactly this class.</param>
/// <param name="Version">The concurrency version.</param>
public sealed record CiClassDto(
    Guid Id,
    string Key,
    string Label,
    string Icon,
    string Color,
    string? ParentKey,
    string? Description,
    bool IsSystem,
    int SortOrder,
    IReadOnlyList<CiAttributeDefinition> Attributes,
    int CiCount,
    long Version);
