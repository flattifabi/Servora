using Servora.Domain.Cmdb;

namespace Servora.Application.Cmdb;

/// <summary>A configuration item with resolved class, people and attribute schema.</summary>
public sealed class CiDetail
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the editable values.</summary>
    public CiInput Values { get; set; } = new();

    /// <summary>Gets or sets the class label.</summary>
    public string ClassLabel { get; set; } = string.Empty;

    /// <summary>Gets or sets the class icon.</summary>
    public string ClassIcon { get; set; } = "box";

    /// <summary>Gets or sets the class color.</summary>
    public string ClassColor { get; set; } = "#7c6cff";

    /// <summary>Gets or sets the class chain labels (root first) for breadcrumbs.</summary>
    public IReadOnlyList<string> ClassPath { get; set; } = [];

    /// <summary>Gets or sets the effective attribute definitions of the class.</summary>
    public IReadOnlyList<CiAttributeDefinition> AttributeDefinitions { get; set; } = [];

    /// <summary>Gets or sets the owner display name.</summary>
    public string? OwnerName { get; set; }

    /// <summary>Gets or sets the support group display name.</summary>
    public string? SupportGroupName { get; set; }

    /// <summary>Gets or sets the discovery source.</summary>
    public string? DiscoverySource { get; set; }

    /// <summary>Gets or sets the last discovery time.</summary>
    public DateTimeOffset? LastDiscoveredAt { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the creator.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the last editor.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Gets or sets the number of upstream relationships (CIs that depend on this CI).</summary>
    public int UpstreamCount { get; set; }

    /// <summary>Gets or sets the number of downstream relationships (CIs this CI depends on).</summary>
    public int DownstreamCount { get; set; }
}
