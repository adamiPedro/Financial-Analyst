using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.FinancialData;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialIntelligence.IntegrationTests.FinancialData;

// Petrobras 2024, consolidated, from the itr_2024 and dfp_2024 fixtures
// (MIL, so × 1000). Quarters worked out by hand in QuarterlyFlowTests.
public sealed class QuarterlyMetricQueryTests : IAsyncLifetime, IDisposable
{
    private const int Petrobras = 9512;
    private const int Itau = 19348;
    private const string Dfp2024 = "dfp_cia_aberta_2024.zip";
    private const string Itr2024 = "itr_cia_aberta_2024.zip";
    private static readonly DateTimeOffset Downloaded = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly[] QuarterEnds = [new(2024, 3, 31), new(2024, 6, 30), new(2024, 9, 30), new(2024, 12, 31)];

    private readonly TestDatabase _database = new();
    private readonly FixtureZip _zips = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _database.AddFixtureCompaniesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    public void Dispose() => _zips.Dispose();

    private async Task ImportAsync(string folder, string zipName, FilingForm form, Func<string, string, IEnumerable<string>>? edit = null)
    {
        await using var db = _database.CreateContext();
        await new StatementImporter(db, NullLogger<StatementImporter>.Instance)
            .ImportAsync(_zips.Create(folder, zipName, edit), form, Downloaded, new HashSet<int> { 9512, 19348, 906, 23159 }, CancellationToken.None);
    }

    private async Task ImportYearAsync()
    {
        await ImportAsync("itr_2024", Itr2024, FilingForm.Itr);
        await ImportAsync("dfp_2024", Dfp2024, FilingForm.Dfp);
    }

    private async Task<MetricSeries> QueryAsync(int cvmCode, Metric metric)
    {
        await using var db = _database.CreateContext();
        return (await new MetricQuery(db, NullLogger<MetricQuery>.Instance).GetQuarterlyAsync(cvmCode, metric, CancellationToken.None))!;
    }

    [Fact]
    public async Task Petrobras_revenue_has_four_quarters_with_q4_calculated()
    {
        await ImportYearAsync();

        var series = await QueryAsync(Petrobras, Metric.Revenue);

        Assert.Equal(AccountingBasis.Consolidated, series.Basis);
        Assert.Equal(QuarterEnds, series.Points.Select(p => p.PeriodEnd));
        Assert.Equal([117_721_000_000m, 122_258_000_000m, 129_582_000_000m, 121_268_000_000m], series.Points.Select(p => p.Value));
        Assert.Equal([false, false, false, true], series.Points.Select(p => p.IsCalculated));
        Assert.Equal([Dfp2024, Itr2024], series.Points[3].Sources.Select(s => s.SourceFile));
    }

    [Fact]
    public async Task Petrobras_cash_flow_quarters_after_q1_are_calculated()
    {
        await ImportYearAsync();

        var series = await QueryAsync(Petrobras, Metric.OperatingCashFlow);

        Assert.Equal([46_481_000_000m, 47_170_000_000m, 62_720_000_000m, 47_666_000_000m], series.Points.Select(p => p.Value));
        Assert.Equal([false, true, true, true], series.Points.Select(p => p.IsCalculated));
    }

    [Fact]
    public async Task Petrobras_net_income_reads_the_shareholders_part_per_quarter()
    {
        // Q2 is a loss (−2,605,000) and Q4 too (36,606,000 − 53,650,000).
        await ImportYearAsync();

        var series = await QueryAsync(Petrobras, Metric.NetIncome);

        Assert.Equal([23_700_000_000m, -2_605_000_000m, 32_555_000_000m, -17_044_000_000m], series.Points.Select(p => p.Value));
    }

    [Fact]
    public async Task A_balance_at_q4_is_the_year_end_figure_as_published()
    {
        await ImportYearAsync();

        var series = await QueryAsync(Petrobras, Metric.TotalAssets);

        Assert.Equal([1_067_292_000_000m, 1_058_688_000_000m, 1_077_849_000_000m, 1_124_797_000_000m], series.Points.Select(p => p.Value));
        Assert.All(series.Points, p => Assert.False(p.IsCalculated));
    }

    [Fact]
    public async Task With_only_the_annual_filing_q4_is_unavailable()
    {
        await ImportAsync("dfp_2024", Dfp2024, FilingForm.Dfp);

        var point = Assert.Single((await QueryAsync(Petrobras, Metric.Revenue)).Points);

        Assert.Equal(MetricPointStatus.Unavailable, point.Status);
        Assert.Null(point.Value);
    }

    [Fact]
    public async Task A_bank_has_no_quarterly_revenue()
    {
        await ImportAsync("dfp_2024", Dfp2024, FilingForm.Dfp);

        var series = await QueryAsync(Itau, Metric.Revenue);

        Assert.True(series.NotApplicable);
        Assert.Equal(MetricPointStatus.NotApplicable, Assert.Single(series.Points).Status);
    }

    [Fact]
    public async Task Q4_sources_carry_each_filings_own_version()
    {
        await ImportYearAsync();
        await ImportAsync("itr_2024", Itr2024, FilingForm.Itr, (_, line) =>
            [line.Replace("33.000.167/0001-01;2024-09-30;1;", "33.000.167/0001-01;2024-09-30;2;", StringComparison.Ordinal)]);

        var q4 = (await QueryAsync(Petrobras, Metric.Revenue)).Points[3];

        Assert.Equal([(Dfp2024, 1), (Itr2024, 2)], q4.Sources.Select(s => (s.SourceFile, s.FilingVersion)));
        Assert.Equal(121_268_000_000m, q4.Value);
    }

    [Fact]
    public async Task A_quarter_filed_only_on_the_other_basis_is_unavailable_and_breaks_the_cash_flow_chain()
    {
        await ImportAsync("dfp_2024", Dfp2024, FilingForm.Dfp);
        await ImportAsync("itr_2024", Itr2024, FilingForm.Itr, (name, line) =>
            name.Contains("_con_", StringComparison.Ordinal)
            && line.StartsWith("33.000.167/0001-01;2024-06-30;", StringComparison.Ordinal) ? [] : [line]);

        var revenue = await QueryAsync(Petrobras, Metric.Revenue);
        var cashFlow = await QueryAsync(Petrobras, Metric.OperatingCashFlow);

        Assert.Equal(AccountingBasis.Consolidated, revenue.Basis);
        Assert.Equal(QuarterEnds, revenue.Points.Select(p => p.PeriodEnd));
        Assert.Equal([MetricPointStatus.Value, MetricPointStatus.Unavailable, MetricPointStatus.Value, MetricPointStatus.Value], revenue.Points.Select(p => p.Status));
        Assert.Equal([117_721_000_000m, null, 129_582_000_000m, 121_268_000_000m], revenue.Points.Select(p => p.Value));
        Assert.Empty(revenue.Points[1].Sources);
        Assert.False(revenue.Points[1].IsCalculated);
        Assert.Equal([MetricPointStatus.Value, MetricPointStatus.Unavailable, MetricPointStatus.Unavailable, MetricPointStatus.Value], cashFlow.Points.Select(p => p.Status));
        Assert.Equal([46_481_000_000m, null, null, 47_666_000_000m], cashFlow.Points.Select(p => p.Value));
    }

    [Fact]
    public async Task Each_source_names_its_period()
    {
        await ImportYearAsync();

        var cashFlow = await QueryAsync(Petrobras, Metric.OperatingCashFlow);
        var revenue = await QueryAsync(Petrobras, Metric.Revenue);
        var assets = await QueryAsync(Petrobras, Metric.TotalAssets);

        Assert.Equal(
            [("6.01", (DateOnly?)new DateOnly(2024, 1, 1), new DateOnly(2024, 6, 30)), ("6.01", new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31))],
            cashFlow.Points[1].Sources.Select(s => (s.AccountCode, s.PeriodStart, s.PeriodEnd)));
        Assert.Equal(
            [("3.01", (DateOnly?)new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), Dfp2024), ("3.01", new DateOnly(2024, 1, 1), new DateOnly(2024, 9, 30), Itr2024)],
            revenue.Points[3].Sources.Select(s => (s.AccountCode, s.PeriodStart, s.PeriodEnd, s.SourceFile)));
        Assert.Null(Assert.Single(assets.Points[3].Sources).PeriodStart);
    }

    [Fact]
    public async Task An_unknown_company_has_no_series()
    {
        await using var db = _database.CreateContext();

        Assert.Null(await new MetricQuery(db, NullLogger<MetricQuery>.Instance).GetQuarterlyAsync(4170, Metric.Revenue, CancellationToken.None));
    }
}
