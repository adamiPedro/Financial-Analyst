using FinancialIntelligence.Application.Ingestion;
using Microsoft.Extensions.Options;

namespace FinancialIntelligence.Worker;

internal sealed partial class IngestionScheduler(
    IIngestionSync sync,
    TimeProvider clock,
    IOptions<IngestionScheduleOptions> options,
    ILogger<IngestionScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.Interval;
        LogStarted(logger, interval);

        // Created before the first run, so the rhythm is set by startup time and
        // doesn't drift by however long the first run (a ~700 MB backfill) takes.
        using var timer = new PeriodicTimer(interval, clock);
        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutdown, not a failure.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await sync.RunAsync(stoppingToken);
        }
        // An exception that escapes ExecuteAsync stops the whole host (the
        // default BackgroundServiceExceptionBehavior since .NET 6), so one bad
        // run would end every later one. Logged instead; the next tick retries.
        catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogRunFailed(logger, e);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingestion scheduler started: a run now, then every {Interval}.")]
    private static partial void LogStarted(ILogger logger, TimeSpan interval);

    [LoggerMessage(Level = LogLevel.Error, Message = "Ingestion run failed; the next scheduled run will try again.")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
