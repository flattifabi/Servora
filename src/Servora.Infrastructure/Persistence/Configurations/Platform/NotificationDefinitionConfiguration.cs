using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Notifications;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="NotificationDefinition"/>.</summary>
internal sealed class NotificationDefinitionConfiguration : IEntityTypeConfiguration<NotificationDefinition>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationDefinition> builder)
    {
        builder.ToTable("sysevent_email_action");
        builder.Property(n => n.Name).HasMaxLength(150).IsRequired();
        builder.Property(n => n.Description).HasMaxLength(1000);
        builder.Property(n => n.TableName).HasMaxLength(64).IsRequired();
        builder.Property(n => n.ConditionJson).HasMaxLength(16_000);
        builder.Property(n => n.Subject).HasMaxLength(500);
        builder.Property(n => n.Body).HasMaxLength(20_000);
        builder.Property(n => n.RecipientFieldsJson).HasMaxLength(2000);
        builder.Property(n => n.RecipientGroupIdsJson).HasMaxLength(4000);
        builder.Property(n => n.RecipientUserIdsJson).HasMaxLength(4000);
        builder.Property(n => n.RecipientEmails).HasMaxLength(2000);
        builder.HasIndex(n => new { n.TableName, n.IsActive });
    }
}
