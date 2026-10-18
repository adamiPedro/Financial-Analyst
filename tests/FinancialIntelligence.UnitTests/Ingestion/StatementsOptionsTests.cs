using FinancialIntelligence.Infrastructure;
using FinancialIntelligence.Infrastructure.Ingestion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace FinancialIntelligence.UnitTests.Ingestion;

public sealed class StatementsOptionsTests
{
    private static readonly DateTimeOffset Today = new(2026, 10, 18, 0, 0, 0, TimeSpan.Zero);
    private static readonly int[] ExpectedCompanies = [9512, 4170];

    private static StatementsOptions Resolve(string? firstYear, params string[] trackedCompanies)
    {
        var settings = new Dictionary<string, string?> { ["Statements:FirstYear"] = firstYear };
        for (var i = 0; i < trackedCompanies.Length; i++)
        {
            settings[$"Statements:TrackedCompanies:{i}"] = trackedCompanies[i];
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(Today));
        services.AddIngestion();

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOptions<StatementsOptions>>().Value;
    }

    [Fact]
    public void The_tracked_list_and_first_year_bind_from_config()
    {
        var options = Resolve("2010", "9512", "4170");

        Assert.Equal(ExpectedCompanies, options.TrackedCompanies);
        Assert.Equal(2010, options.FirstYear);
    }

    [Fact]
    public void An_empty_tracked_list_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("2010"));

    [Fact]
    public void A_tracked_code_of_zero_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("2010", "9512", "0"));

    [Fact]
    public void A_first_year_before_cvm_publishes_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("2009", "9512"));

    [Fact]
    public void A_first_year_after_the_current_year_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve("2027", "9512"));

    [Fact]
    public void A_missing_first_year_is_refused() =>
        Assert.Throws<OptionsValidationException>(() => Resolve(null, "9512"));
}
