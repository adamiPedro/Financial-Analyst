using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Infrastructure.Companies;
using FinancialIntelligence.Infrastructure.FinancialData;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FinancialIntelligence.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>
    /// Infrastructure registers its own services. The alternative - calling
    /// AddDbContext and UseNpgsql from Program.cs - would drag EF Core and
    /// Npgsql into the Api project's package list for no benefit. Api asks for
    /// infrastructure; it does not need to know infrastructure is EF Core.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        // snake_case names so hand-written SQL, psql and COPY work without
        // quoting: Postgres folds unquoted identifiers to lower case, and EF's
        // default PascalCase names would otherwise need "Quotes" everywhere.
        services.AddDbContext<FinancialIntelligenceDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IRevenueQuery, RevenueQuery>();
        services.AddScoped<RevenueImporter>();
        services.AddScoped<CompanyImporter>();

        return services;
    }
}
