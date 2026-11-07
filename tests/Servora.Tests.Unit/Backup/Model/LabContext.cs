using Microsoft.EntityFrameworkCore;

namespace Servora.Tests.Unit.Backup.Model;

/// <summary>
/// Test model without migrations (EnsureCreated mode) containing a foreign key cycle (department ↔ manager), a nullable
/// self reference (mentor), an auto-increment key and many value types.
/// </summary>
/// <param name="options">The options.</param>
internal sealed class LabContext(DbContextOptions<LabContext> options) : DbContext(options)
{
    /// <summary>Creates a context for a SQLite connection string.</summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The context.</returns>
    public static LabContext Create(string connectionString) =>
        new(new DbContextOptionsBuilder<LabContext>().UseSqlite(connectionString).Options);

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LabDepartment>(b =>
        {
            b.ToTable("Departments");
            b.Property(d => d.Name).HasMaxLength(100).IsRequired();
            b.HasOne(d => d.Manager).WithMany().HasForeignKey(d => d.ManagerId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LabEmployee>(b =>
        {
            b.ToTable("Employees");
            b.Property(e => e.Name).HasMaxLength(100).IsRequired();
            b.HasOne(e => e.Department).WithMany().HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(e => e.Mentor).WithMany().HasForeignKey(e => e.MentorId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<LabBadge>(b =>
        {
            b.ToTable("Badges");
            b.Property(x => x.Code).HasMaxLength(50).IsRequired();
            b.HasOne<LabEmployee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
