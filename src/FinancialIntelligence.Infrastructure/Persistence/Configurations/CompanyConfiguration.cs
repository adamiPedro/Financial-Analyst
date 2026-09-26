using FinancialIntelligence.Domain.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FinancialIntelligence.Infrastructure.Persistence.Configurations;

/// <summary>
/// Mapping lives here, not on the entity. Company carries no EF attributes and
/// no reference to EF Core, which is what lets Domain reference nothing at all.
/// </summary>
internal sealed class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.CvmCode);

        // The critical line. CVM assigns this code; we only record it. EF's
        // convention for an integer key is an identity column, which would make
        // Postgres generate a value and throw the real CD_CVM away - and the
        // failure is silent, because an auto-generated key looks perfectly valid.
        builder.Property(c => c.CvmCode).ValueGeneratedNever();

        builder.Property(c => c.Cnpj)
            .HasConversion(cnpj => cnpj.Value, value => Cnpj.Parse(value))
            .HasMaxLength(14)
            .IsRequired();

        // CNPJ is not the key but must still be unique: two CD_CVM rows sharing a
        // CNPJ would mean the registry was misparsed, and it is better to fail the
        // insert than to quietly double-count a company.
        builder.HasIndex(c => c.Cnpj)
            .IsUnique()
            .HasDatabaseName("ix_companies_cnpj");

        builder.Property(c => c.Name)
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Ticker).HasMaxLength(10);

        // Postgres permits multiple NULLs in a unique index, which is exactly the
        // behaviour wanted here: tickers must not collide, but many companies will
        // have none until B3 data is loaded.
        builder.HasIndex(c => c.Ticker)
            .IsUnique()
            .HasDatabaseName("ix_companies_ticker");

        builder.Property(c => c.Sector).HasMaxLength(100);
        builder.Property(c => c.RegistrationStatus).HasMaxLength(40);
    }
}
