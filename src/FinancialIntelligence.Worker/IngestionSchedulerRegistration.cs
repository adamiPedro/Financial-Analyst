using Microsoft.Extensions.Options;

namespace FinancialIntelligence.Worker;

internal static class IngestionSchedulerRegistration
{
    public static IServiceCollection AddIngestionScheduler(this IServiceCollection services)
    {
        // Checked at boot. PeriodicTimer rejects a zero interval and anything over
        // about 49.7 days (UInt32.MaxValue - 1 ms), but only once ExecuteAsync runs,
        // which is later and as a host crash. 49 days keeps clear of that limit.
        services.AddOptions<IngestionScheduleOptions>()
            .BindConfiguration(IngestionScheduleOptions.Section)
            .Validate(o => o.Interval > TimeSpan.Zero && o.Interval <= TimeSpan.FromDays(49),
                "Ingestion:Interval must be greater than zero and at most 49 days.")
            .ValidateOnStart();

        services.AddHostedService<IngestionScheduler>();
        return services;
    }
}
