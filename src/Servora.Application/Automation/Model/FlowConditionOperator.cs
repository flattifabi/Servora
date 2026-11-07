namespace Servora.Application.Automation.Model;

/// <summary>Comparison operator of a <see cref="FlowConditionRule"/>.</summary>
public enum FlowConditionOperator
{
    /// <summary>Values are equal (numbers numerically, dates chronologically, text case-insensitively).</summary>
    Equals = 0,

    /// <summary>Values are not equal.</summary>
    NotEquals = 1,

    /// <summary>Text contains the right value.</summary>
    Contains = 2,

    /// <summary>Text does not contain the right value.</summary>
    NotContains = 3,

    /// <summary>Text starts with the right value.</summary>
    StartsWith = 4,

    /// <summary>Text ends with the right value.</summary>
    EndsWith = 5,

    /// <summary>Left is greater than right.</summary>
    GreaterThan = 6,

    /// <summary>Left is greater than or equal to right.</summary>
    GreaterOrEqual = 7,

    /// <summary>Left is less than right.</summary>
    LessThan = 8,

    /// <summary>Left is less than or equal to right.</summary>
    LessOrEqual = 9,

    /// <summary>Left is empty.</summary>
    IsEmpty = 10,

    /// <summary>Left is not empty.</summary>
    IsNotEmpty = 11,

    /// <summary>Left is one of the comma separated right values.</summary>
    In = 12,

    /// <summary>Left is none of the comma separated right values.</summary>
    NotIn = 13,
}
