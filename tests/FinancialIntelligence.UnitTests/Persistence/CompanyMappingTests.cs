using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace FinancialIntelligence.UnitTests.Persistence;

public class CompanyMappingTests
{
    [Fact]
    public void Company_key_is_never_generated_by_the_database()
    {
        // ADR 0007. Without ValueGeneratedNever, EF maps an int key as an identity
        // column and Postgres replaces the real CD_CVM with its own sequence value.
        // Nothing errors, so only a test catches it being removed.
        // Building the model never opens a connection, so no Postgres is needed.
        var options = new DbContextOptionsBuilder<FinancialIntelligenceDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new FinancialIntelligenceDbContext(options);

        var key = context.Model.FindEntityType(typeof(Company))!.FindPrimaryKey()!;

        var property = Assert.Single(key.Properties);
        Assert.Equal(nameof(Company.CvmCode), property.Name);
        Assert.Equal(ValueGenerated.Never, property.ValueGenerated);
    }
}
