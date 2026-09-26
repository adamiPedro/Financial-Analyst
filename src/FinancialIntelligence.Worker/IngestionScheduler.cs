namespace FinancialIntelligence.Worker;

/// <summary>
/// Placeholder. Scheduled ingestion arrives at M1; this exists so the Worker
/// host builds and runs from M0 onward.
/// </summary>
internal sealed partial class IngestionScheduler(ILogger<IngestionScheduler> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingestion scheduler started. No jobs registered yet (M0).")]
    private static partial void LogStarted(ILogger logger);
}
