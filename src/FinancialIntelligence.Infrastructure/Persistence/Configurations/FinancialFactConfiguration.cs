using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Domain.FinancialData;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialIntelligence.Infrastructure.Persistence.Configurations;

internal sealed class FinancialFactConfiguration : IEntityTypeConfiguration<FinancialFact>
{
    public void Configure(EntityTypeBuilder<FinancialFact> builder)
    {
        builder.HasKey(f => f.Id);

        builder.HasOne<Company>()
            .WithMany()
            .HasForeignKey(f => f.CvmCode)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(f => f.AccountCode).HasMaxLength(20).IsRequired();

        // Stored as text so rows read clearly in psql; the enum's numbers mean
        // nothing outside this codebase.
        builder.Property(f => f.Basis).HasConversion<string>().HasMaxLength(20);

        builder.Property(f => f.SourceFile).HasMaxLength(255).IsRequired();

        // What makes importing idempotent: the same filing version of the same
        // value can exist once. A second import of the same file inserts nothing,
        // and a resubmission (higher VERSAO) lands beside the original.
        builder.HasIndex(f => new
            {
                f.CvmCode,
                f.AccountCode,
                f.Basis,
                f.PeriodStart,
                f.PeriodEnd,
                f.Version
            })
            .IsUnique()
            .HasDatabaseName("ux_financial_facts_value_identity");
    }
}
