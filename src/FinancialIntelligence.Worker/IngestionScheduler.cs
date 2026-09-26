namespace FinancialIntelligence.Worker;

/// <summary>
/// Placeholder. Scheduled ingestion arrives at M1; this exists so the Worker
/// host builds and runs from M0 onward.
/// </summary>
internal sealed class IngestionScheduler(ILogger<IngestionScheduler> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Ingestion scheduler started. No jobs registered yet (M0).");
        return Task.CompletedTask;
    }
}
