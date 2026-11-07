using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Configuration;
using Servora.Infrastructure.Configuration;

namespace Servora.Infrastructure.Persistence.Configurations.Configuration;

/// <summary>EF Core mapping of <see cref="OutboundEmail"/> (mail outbox).</summary>
internal sealed class OutboundEmailConfiguration : IEntityTypeConfiguration<OutboundEmail>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<OutboundEmail> builder)
    {
        builder.ToTable("sys_email_outbox");
        builder.Property(e => e.Recipients).HasMaxLength(OutboundEmailLimits.MaxRecipientsLength).IsRequired();
        builder.Property(e => e.CcRecipients).HasMaxLength(OutboundEmailLimits.MaxRecipientsLength);
        builder.Property(e => e.Subject).HasMaxLength(OutboundEmailLimits.MaxSubjectLength).IsRequired();
        builder.Property(e => e.HtmlBody).HasMaxLength(OutboundEmailLimits.MaxBodyLength).IsRequired();
        builder.Property(e => e.TextBody).HasMaxLength(OutboundEmailLimits.MaxBodyLength);
        builder.Property(e => e.LastError).HasMaxLength(OutboundEmailLimits.MaxErrorLength);
        builder.Property(e => e.Source).HasMaxLength(64);
        builder.HasIndex(e => new { e.Status, e.NextAttemptAt });
        builder.HasIndex(e => e.CreatedAt);
    }
}
