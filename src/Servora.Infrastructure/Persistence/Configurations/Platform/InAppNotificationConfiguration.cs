using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Notifications;

namespace Servora.Infrastructure.Persistence.Configurations.Platform;

/// <summary>EF Core mapping of <see cref="InAppNotification"/>.</summary>
internal sealed class InAppNotificationConfiguration : IEntityTypeConfiguration<InAppNotification>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<InAppNotification> builder)
    {
        builder.ToTable("sys_notification_inbox");
        builder.Property(n => n.Title).HasMaxLength(300).IsRequired();
        builder.Property(n => n.Body).HasMaxLength(2000);
        builder.Property(n => n.Link).HasMaxLength(500);
        builder.Property(n => n.TableName).HasMaxLength(64);
        builder.HasIndex(n => new { n.UserId, n.ReadAt });
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
    }
}
