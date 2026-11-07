using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Notifications;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="NotificationTemplate"/>.</summary>
internal sealed class NotificationTemplateConfiguration : IEntityTypeConfiguration<NotificationTemplate>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<NotificationTemplate> builder)
    {
        builder.ToTable("sysevent_email_template");
        builder.Property(t => t.Name).HasMaxLength(150).IsRequired();
        builder.HasIndex(t => t.Name).IsUnique();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Subject).HasMaxLength(500).IsRequired();
        builder.Property(t => t.Body).HasMaxLength(20_000).IsRequired();
    }
}
