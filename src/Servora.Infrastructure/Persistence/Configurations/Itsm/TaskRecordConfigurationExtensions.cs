using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Itsm;

namespace Servora.Infrastructure.Persistence.Configurations.Itsm;

/// <summary>
/// Shared EF Core mapping for every <see cref="TaskRecord"/> subtype. Call <see cref="ConfigureTaskRecord{T}"/> first in
/// each subtype configuration so that all task tables share identical column sizes and indexes.
/// </summary>
public static class TaskRecordConfigurationExtensions
{
    /// <summary>Applies the common task mapping.</summary>
    /// <typeparam name="T">The task subtype.</typeparam>
    /// <param name="builder">The entity builder.</param>
    /// <param name="tableName">The physical table name (normally the technical table name).</param>
    /// <returns>The builder for chaining.</returns>
    public static EntityTypeBuilder<T> ConfigureTaskRecord<T>(this EntityTypeBuilder<T> builder, string tableName)
        where T : TaskRecord
    {
        builder.ToTable(tableName);
        builder.Property(t => t.Number).HasMaxLength(40).IsRequired();
        builder.HasIndex(t => t.Number).IsUnique();
        builder.Property(t => t.ShortDescription).HasMaxLength(300).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(32_000);
        builder.Property(t => t.Tags).HasMaxLength(1000);
        builder.Property(t => t.CustomFieldsJson).HasMaxLength(32_000);
        builder.HasIndex(t => t.AssignmentGroupId);
        builder.HasIndex(t => t.AssignedToId);
        builder.HasIndex(t => t.OpenedById);
        builder.HasIndex(t => new { t.Active, t.Priority });
        builder.HasIndex(t => t.ConfigurationItemId);
        return builder;
    }
}
