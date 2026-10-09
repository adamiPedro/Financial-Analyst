using FinancialIntelligence.Domain.Ingestion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class SourceFileDownloadConfiguration : IEntityTypeConfiguration<SourceFileDownload>
{
    public void Configure(EntityTypeBuilder<SourceFileDownload> builder)
    {
        builder.HasKey(d => d.Id);

        builder.Property(d => d.FileName).HasMaxLength(100).IsRequired();
        builder.Property(d => d.Url).HasMaxLength(255).IsRequired();

        // The naming convention would make this "e_tag".
        builder.Property(d => d.ETag).HasColumnName("etag").HasMaxLength(100);

        builder.Property(d => d.Sha256).HasMaxLength(64).IsRequired();

        // Every download asks "what did we last get for this file?".
        builder.HasIndex(d => new { d.FileName, d.DownloadedAt })
            .HasDatabaseName("ix_source_file_downloads_file_name_downloaded_at");
    }
}
