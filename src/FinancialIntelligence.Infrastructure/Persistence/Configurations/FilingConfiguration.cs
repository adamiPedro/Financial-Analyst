using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Domain.FinancialData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class FilingConfiguration : IEntityTypeConfiguration<Filing>
{
    public void Configure(EntityTypeBuilder<Filing> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(f => f.CvmCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.Form).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Basis).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.Layout).HasConversion<string>().HasMaxLength(20);
        builder.Property(f => f.SourceFile).HasMaxLength(255).IsRequired();

        // One row per filing CVM published. A resubmission has a higher version,
        // so it lands beside the original instead of colliding with it.
        builder.HasIndex(f => new { f.CvmCode, f.Form, f.ReferenceDate, f.Version, f.Basis })
            .IsUnique()
            .HasDatabaseName("ux_filings_identity");
    }
}
