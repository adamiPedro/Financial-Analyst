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
        services.AddDbContext<FinancialIntelligenceDbContext>(options =>
            options.UseNpgsql(connectionString));

        return services;
    }
}
