namespace Servora.Tests.Unit.Backup.Model;

/// <summary>Test entity: a department whose optional manager is an employee (closes a foreign key cycle).</summary>
internal sealed class LabDepartment
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the manager (nullable foreign key to <see cref="LabEmployee"/>).</summary>
    public Guid? ManagerId { get; set; }

    /// <summary>Gets or sets the manager.</summary>
    public LabEmployee? Manager { get; set; }
}
