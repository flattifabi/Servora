namespace Servora.Domain.Itsm;

/// <summary>Breadth of the business impact of a task record.</summary>
public enum Impact
{
    /// <summary>Enterprise-wide or many users affected.</summary>
    High = 1,

    /// <summary>A department or group of users affected.</summary>
    Medium = 2,

    /// <summary>A single user affected.</summary>
    Low = 3,
}
