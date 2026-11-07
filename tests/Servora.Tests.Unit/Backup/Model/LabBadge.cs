namespace Servora.Tests.Unit.Backup.Model;

/// <summary>Test entity with an auto-increment integer key and many value types.</summary>
internal sealed class LabBadge
{
    /// <summary>Gets or sets the auto-increment identifier.</summary>
    public int Id { get; set; }

    /// <summary>Gets or sets the owning employee.</summary>
    public Guid EmployeeId { get; set; }

    /// <summary>Gets or sets the code (unicode text).</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Gets or sets a binary photo.</summary>
    public byte[]? Photo { get; set; }

    /// <summary>Gets or sets the issue time.</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>Gets or sets a decimal score.</summary>
    public decimal Score { get; set; }

    /// <summary>Gets or sets a flag.</summary>
    public bool Active { get; set; }

    /// <summary>Gets or sets an optional date.</summary>
    public DateOnly? ValidUntil { get; set; }

    /// <summary>Gets or sets a floating point value.</summary>
    public double Ratio { get; set; }

    /// <summary>Gets or sets an optional duration.</summary>
    public TimeSpan? Shift { get; set; }
}
