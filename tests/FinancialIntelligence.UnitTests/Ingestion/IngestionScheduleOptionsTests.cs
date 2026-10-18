using FinancialIntelligence.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace FinancialIntelligence.UnitTests.Ingestion;

public sealed class IngestionScheduleOptionsTests
{
    private static IngestionScheduleOptions Resolve(string? interval)
    {
        var settings = new Dictionary<string, string?>();
        if (interval is not null)
        {
            settings["Ingestion:Interval"] = interval;
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddIngestionScheduler();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<IngestionScheduleOptions>>().Value;
    }

    [Fact]
    public void Without_a_setting_it_runs_once_a_day() =>
        Assert.Equal(TimeSpan.FromHours(24), Resolve(null).Interval);

    [Fact]
    public void The_interval_binds_from_config() =>
        Assert.Equal(TimeSpan.FromHours(6), Resolve("06:00:00").Interval);

    [Fact]
    public void A_zero_interval_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("00:00:00"));

    [Fact]
    public void An_interval_longer_than_the_timer_allows_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("60.00:00:00"));
}
