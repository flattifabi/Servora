using Servora.Domain.Common;

namespace Servora.Domain.Platform;

/// <summary>Counter used to generate human readable record numbers such as <c>INC0001001</c>.</summary>
public class NumberSequence : Entity
{
    /// <summary>Gets or sets the technical table name the sequence belongs to.</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Gets or sets the number prefix, for example <c>INC</c>.</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>Gets or sets the next numeric value to hand out.</summary>
    public long NextValue { get; set; } = 1001;

    /// <summary>Gets or sets the zero-padded number of digits.</summary>
    public int Digits { get; set; } = 7;

    /// <summary>Gets or sets the concurrency token used to make number allocation safe across processes.</summary>
    public long Version { get; set; }
}
