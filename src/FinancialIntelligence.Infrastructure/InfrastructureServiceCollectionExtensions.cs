using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Application.Ingestion;
using FinancialIntelligence.Infrastructure.Companies;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.FinancialData;
using FinancialIntelligence.Infrastructure.Ingestion;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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

        services.AddScoped<CompanyImporter>();
        services.AddScoped<StatementImporter>();
        services.AddScoped<IMetricQuery, MetricQuery>();

        services.AddOptions<CvmOptions>().BindConfiguration(CvmOptions.Section);

        services.AddHttpClient<CvmDownloader>((provider, client) =>
            {
                client.BaseAddress = provider.GetRequiredService<IOptions<CvmOptions>>().Value.BaseUrl;

                // The resilience handler owns the time limits. HttpClient.Timeout
                // wraps the whole pipeline, retries included, so the 100-second
                // default would cap every attempt and retry together, far below
                // the handler's 5 min / 15 min limits, and fail with its own exception.
                client.Timeout = Timeout.InfiniteTimeSpan;
            })
            .AddStandardResilienceHandler(resilience =>
            {
                resilience.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);
                resilience.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(15);

                // The library refuses to start unless this is at least twice the
                // attempt timeout - it has to see a few attempts to judge a failure rate.
                resilience.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
            });

        return services;
    }

    /// <summary>
    /// Separate from AddInfrastructure because only the Worker syncs. The Api
    /// calls AddInfrastructure too, and ValidateOnStart would otherwise make it
    /// refuse to start for want of a tracked list it never reads.
    /// </summary>
    public static IServiceCollection AddIngestion(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);

        services.AddOptions<StatementsOptions>()
            .BindConfiguration(StatementsOptions.Section)
            .Validate(
                o => o.TrackedCompanies.Count > 0 && o.TrackedCompanies.All(code => code > 0),
                "Statements:TrackedCompanies must list at least one CVM code, all positive.")
            .Validate<TimeProvider>(
                (o, clock) => o.FirstYear >= CvmFile.FirstDfpYear && o.FirstYear <= clock.GetUtcNow().Year,
                $"Statements:FirstYear must be between {CvmFile.FirstDfpYear} and the current year.")
            .ValidateOnStart();

        // Singleton: it holds nothing per run. Each file gets its own scope inside.
        services.AddSingleton<IIngestionSync, IngestionSync>();

        return services;
    }
}
