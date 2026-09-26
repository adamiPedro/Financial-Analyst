using FinancialIntelligence.Domain.Companies;
using Microsoft.EntityFrameworkCore;

namespace FinancialIntelligence.Infrastructure.Persistence;

public sealed class FinancialIntelligenceDbContext(
    DbContextOptions<FinancialIntelligenceDbContext> options)
    : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Discovers every IEntityTypeConfiguration in this assembly rather than
        // listing them here. One less place to forget to register a new entity.
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(FinancialIntelligenceDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
