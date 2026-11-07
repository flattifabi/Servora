namespace Servora.Domain.Catalog;

/// <summary>Input type of a catalog variable.</summary>
public enum CatalogVariableType
{
    /// <summary>Single line text.</summary>
    Text = 0,

    /// <summary>Multi line text.</summary>
    MultilineText = 1,

    /// <summary>Decimal number.</summary>
    Number = 2,

    /// <summary>Selection from a list of options.</summary>
    Choice = 3,

    /// <summary>Yes / no checkbox.</summary>
    Boolean = 4,

    /// <summary>Calendar date.</summary>
    Date = 5,

    /// <summary>Reference to a user.</summary>
    UserReference = 6,
}
