namespace Servora.Tests.Unit.Backup.Model;

/// <summary>Test entity: an employee with a required department and a nullable self reference (mentor).</summary>
internal sealed class LabEmployee
{
    /// <summary>Gets or sets the identifier.</summary>
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the department (required foreign key).</summary>
    public Guid DepartmentId { get; set; }

    /// <summary>Gets or sets the department.</summary>
    public LabDepartment? Department { get; set; }

    /// <summary>Gets or sets the mentor (nullable self reference).</summary>
    public Guid? MentorId { get; set; }

    /// <summary>Gets or sets the mentor.</summary>
    public LabEmployee? Mentor { get; set; }
}
