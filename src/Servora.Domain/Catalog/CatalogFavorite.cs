using Servora.Domain.Common;

namespace Servora.Domain.Catalog;

/// <summary>A catalog item a user saved as a favourite in the self-service portal.</summary>
public class CatalogFavorite : Entity
{
    /// <summary>Gets or sets the user.</summary>
    [Reference(TableNames.User)]
    public Guid UserId { get; set; }

    /// <summary>Gets or sets the catalog item.</summary>
    [Reference(CatalogTables.Item)]
    public Guid CatalogItemId { get; set; }

    /// <summary>Gets or sets the UTC time the favourite was added.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
