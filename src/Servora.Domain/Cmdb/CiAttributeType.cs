namespace Servora.Domain.Cmdb;

/// <summary>Data type of a class-specific CI attribute.</summary>
public enum CiAttributeType
{
    /// <summary>Single line text.</summary>
    Text = 1,

    /// <summary>Number (integer or decimal).</summary>
    Number = 2,

    /// <summary>Yes / no.</summary>
    Boolean = 3,

    /// <summary>Calendar date (ISO <c>yyyy-MM-dd</c>).</summary>
    Date = 4,

    /// <summary>One value of a fixed list of choices.</summary>
    Choice = 5,

    /// <summary>Multi-line text.</summary>
    LongText = 6,
}
