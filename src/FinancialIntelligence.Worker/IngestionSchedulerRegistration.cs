using Microsoft.Extensions.Options;

namespace FinancialIntelligence.Worker;

internal static class IngestionSchedulerRegistration
{
    public static IServiceCollection AddIngestionScheduler(this IServiceCollection services)
    {
        // Checked at boot. PeriodicTimer would also reject a zero interval, but
        // only once ExecuteAsync runs, which is later and as a host crash.
        services.AddOptions<IngestionScheduleOptions>()
            .BindConfiguration(IngestionScheduleOptions.Section)
            .Validate(o => o.Interval > TimeSpan.Zero, "Ingestion:Interval must be greater than zero.")
            .ValidateOnStart();

        services.AddHostedService<IngestionScheduler>();
        return services;
    }
}
