using FinancialIntelligence.Application.Ingestion;
using FinancialIntelligence.Worker;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FinancialIntelligence.UnitTests.Ingestion;

public sealed class IngestionSchedulerTests : IDisposable
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly DateTimeOffset Start = new(2026, 10, 18, 3, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _clock = new(Start);
    private readonly FakeLogger<IngestionScheduler> _logger = new();
    private readonly FakeSync _sync;

    public IngestionSchedulerTests() => _sync = new FakeSync(_clock);

    public void Dispose() => _sync.Dispose();

    private IngestionScheduler Scheduler() =>
        new(_sync, _clock, Options.Create(new IngestionScheduleOptions { Interval = Interval }), _logger);

    private List<FakeLogRecord> Problems() =>
        _logger.Collector.GetSnapshot().Where(r => r.Level >= LogLevel.Warning).ToList();

    [Fact]
    public async Task It_runs_once_at_startup_without_waiting_for_the_interval()
    {
        using var scheduler = Scheduler();

        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        Assert.Equal(Start, Assert.Single(_sync.RunTimes));
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task It_runs_again_when_the_interval_has_passed_and_not_before()
    {
        using var scheduler = Scheduler();
        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        _clock.Advance(Interval - TimeSpan.FromSeconds(1));
        Assert.Single(_sync.RunTimes);

        _clock.Advance(TimeSpan.FromSeconds(1));
        await _sync.WaitForRunAsync(2);

        Assert.Equal(Start + Interval, _sync.RunTimes[1]);
        await scheduler.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task A_run_that_throws_is_logged_and_the_next_run_still_happens()
    {
        _sync.FailWith = new InvalidOperationException("CVM is having a day.");
        using var scheduler = Scheduler();
        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        _clock.Advance(Interval);
        await _sync.WaitForRunAsync(2);
        await scheduler.StopAsync(CancellationToken.None);

        Assert.True(scheduler.ExecuteTask!.IsCompletedSuccessfully);
        var errors = Problems();
        Assert.Equal(2, errors.Count);
        Assert.All(errors, r => Assert.Same(_sync.FailWith, r.Exception));
    }

    [Fact]
    public async Task Stopping_between_runs_ends_quietly()
    {
        using var scheduler = Scheduler();
        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        await scheduler.StopAsync(CancellationToken.None);

        Assert.True(scheduler.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(Problems());
    }

    [Fact]
    public async Task Stopping_during_a_run_ends_quietly()
    {
        _sync.HangUntilCancelled = true;
        using var scheduler = Scheduler();
        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        await scheduler.StopAsync(CancellationToken.None);

        Assert.True(scheduler.ExecuteTask!.IsCompletedSuccessfully);
        Assert.Empty(Problems());
    }

    [Fact]
    public async Task A_run_that_fails_with_a_real_error_while_stopping_is_still_logged()
    {
        _sync.HangUntilCancelled = true;
        _sync.FailWhenCancelled = new InvalidOperationException("Connection reset while unwinding.");
        using var scheduler = Scheduler();
        await scheduler.StartAsync(CancellationToken.None);
        await _sync.WaitForRunAsync(1);

        await scheduler.StopAsync(CancellationToken.None);

        Assert.True(scheduler.ExecuteTask!.IsCompletedSuccessfully);
        var error = Assert.Single(Problems());
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.Same(_sync.FailWhenCancelled, error.Exception);
    }

    private sealed class FakeSync(TimeProvider clock) : IIngestionSync, IDisposable
    {
        private readonly SemaphoreSlim _started = new(0);
        private int _seen;

        public List<DateTimeOffset> RunTimes { get; } = [];

        public Exception? FailWith { get; set; }

        public bool HangUntilCancelled { get; set; }

        public Exception? FailWhenCancelled { get; set; }

        public async Task<IngestionSyncResult> RunAsync(CancellationToken cancellationToken)
        {
            RunTimes.Add(clock.GetUtcNow());
            _started.Release();

            if (HangUntilCancelled)
            {
                try
                {
                    await Task.Delay(Timeout.Infinite, cancellationToken);
                }
                catch (OperationCanceledException) when (FailWhenCancelled is not null)
                {
                    throw FailWhenCancelled;
                }
            }

            if (FailWith is not null)
            {
                throw FailWith;
            }

            return new IngestionSyncResult(0, 0, 0, 0);
        }

        public async Task WaitForRunAsync(int number)
        {
            for (; _seen < number; _seen++)
            {
                Assert.True(await _started.WaitAsync(TimeSpan.FromSeconds(10)), $"Run {_seen + 1} never started.");
            }
        }

        public void Dispose() => _started.Dispose();
    }
}
