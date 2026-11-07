using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.BusinessRules;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="BusinessRule"/>.</summary>
internal sealed class BusinessRuleConfiguration : IEntityTypeConfiguration<BusinessRule>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BusinessRule> builder)
    {
        builder.ToTable("sys_script");
        builder.Property(r => r.Name).HasMaxLength(150).IsRequired();
        builder.Property(r => r.Description).HasMaxLength(2000);
        builder.Property(r => r.TableName).HasMaxLength(64).IsRequired();
        builder.Property(r => r.ConditionJson).HasMaxLength(16_000);
        builder.Property(r => r.ActionsJson).HasMaxLength(16_000).IsRequired();
        builder.HasIndex(r => new { r.TableName, r.IsActive });
    }
}
