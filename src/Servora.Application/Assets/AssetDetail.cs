using Servora.Domain.Assets;

namespace Servora.Application.Assets;

/// <summary>An asset with resolved references.</summary>
public sealed class AssetDetail
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; }

    /// <summary>Gets or sets the number.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>Gets or sets the current lifecycle state.</summary>
    public AssetState State { get; set; }

    /// <summary>Gets or sets the editable values.</summary>
    public AssetInput Values { get; set; } = new();

    /// <summary>Gets or sets the assignee's display name.</summary>
    public string? AssignedToName { get; set; }

    /// <summary>Gets or sets the linked CI's name.</summary>
    public string? ConfigurationItemName { get; set; }

    /// <summary>Gets or sets the UTC retirement time.</summary>
    public DateTimeOffset? RetiredAt { get; set; }

    /// <summary>Gets or sets the creation time.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Gets or sets the creator.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Gets or sets the last update time.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Gets or sets the last editor.</summary>
    public string? UpdatedBy { get; set; }

    /// <summary>Gets or sets the states the asset can move to.</summary>
    public IReadOnlyList<AssetState> AllowedTransitions { get; set; } = [];
}
