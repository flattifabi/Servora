using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Servora.Domain.Activity;

namespace Servora.Infrastructure.Persistence.Configurations.Activity;

/// <summary>EF Core mapping of <see cref="Attachment"/>.</summary>
internal sealed class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.ToTable("sys_attachment");
        builder.Property(a => a.TableName).HasMaxLength(100).IsRequired();
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.ContentType).HasMaxLength(255).IsRequired();
        builder.Property(a => a.StorageKey).HasMaxLength(64).IsRequired();
        builder.Property(a => a.Sha256).HasMaxLength(64);
        builder.Property(a => a.UploadedBy).HasMaxLength(256).IsRequired();
        builder.HasIndex(a => new { a.TableName, a.RecordId });
    }
}
