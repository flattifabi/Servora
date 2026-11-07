namespace Servora.Application.Conditions;

/// <summary>Comparison operator of a <see cref="ConditionRule"/>.</summary>
public enum ConditionOperator
{
    /// <summary>Equal to the value.</summary>
    Equals = 0,

    /// <summary>Not equal to the value.</summary>
    NotEquals = 1,

    /// <summary>Text contains the value (case-insensitive).</summary>
    Contains = 2,

    /// <summary>Text does not contain the value.</summary>
    NotContains = 3,

    /// <summary>Text starts with the value.</summary>
    StartsWith = 4,

    /// <summary>Text ends with the value.</summary>
    EndsWith = 5,

    /// <summary>Greater than the value (numbers, dates, ordered enums).</summary>
    GreaterThan = 6,

    /// <summary>Greater than or equal to the value.</summary>
    GreaterOrEqual = 7,

    /// <summary>Less than the value.</summary>
    LessThan = 8,

    /// <summary>Less than or equal to the value.</summary>
    LessOrEqual = 9,

    /// <summary>Field is null or empty.</summary>
    IsEmpty = 10,

    /// <summary>Field has a value.</summary>
    IsNotEmpty = 11,

    /// <summary>Value is one of a comma separated list.</summary>
    In = 12,

    /// <summary>Value is none of a comma separated list.</summary>
    NotIn = 13,

    /// <summary>Field changed in this update (record events only).</summary>
    Changed = 14,

    /// <summary>Field changed to the value in this update (record events only).</summary>
    ChangedTo = 15,

    /// <summary>Field changed from the value in this update (record events only).</summary>
    ChangedFrom = 16,

    /// <summary>Reference field equals the current user (<c>javascript:gs.getUserID()</c> equivalent).</summary>
    IsMe = 17,
}
