namespace Servora.Domain.Platform;

/// <summary>
/// Data type of an administrator-defined custom field. The numeric values mirror the application layer's
/// <c>FieldType</c> enumeration so both can be converted by casting.
/// </summary>
public enum CustomFieldType
{
    /// <summary>Single-line text.</summary>
    String = 0,

    /// <summary>Multi-line text.</summary>
    Text = 1,

    /// <summary>Whole number.</summary>
    Integer = 2,

    /// <summary>Decimal number.</summary>
    Decimal = 3,

    /// <summary>True / false.</summary>
    Boolean = 4,

    /// <summary>Date and time (UTC).</summary>
    DateTime = 5,

    /// <summary>Date only.</summary>
    Date = 6,

    /// <summary>One value from a list of choices.</summary>
    Choice = 7,

    /// <summary>Reference to a record of another table.</summary>
    Reference = 8,

    /// <summary>Unique identifier.</summary>
    Guid = 9,

    /// <summary>E-mail address.</summary>
    Email = 10,

    /// <summary>URL (http / https only).</summary>
    Url = 11,

    /// <summary>JSON document.</summary>
    Json = 12,
}
