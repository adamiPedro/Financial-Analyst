using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.FinancialData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialIntelligence.IntegrationTests.FinancialData;

// Expected values are read by hand from the fixture rows (MIL, so × 1000);
// see Fixtures/Cvm/README.md.
public sealed class MetricQueryTests : IAsyncLifetime, IDisposable
{
    private const int Petrobras = 9512;
    private const int Itau = 19348;
    private const int Bradesco = 906;
    private const string Dfp2024 = "dfp_cia_aberta_2024.zip";
    private const string PetrobrasRow = "33.000.167/0001-01;2024-12-31;1;";
    private const string IncomeCon = "dfp_cia_aberta_DRE_con_2024.csv";
    private static readonly DateTimeOffset Downloaded = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly YearEnd2024 = new(2024, 12, 31);
    private static readonly string[] PetrobrasEquitySources = ["2.03", "2.03.09"];
    private static readonly string[] PetrobrasNetIncomeSources = ["3.11.01"];
    private static readonly string[] ItauEquitySources = ["2.08", "2.08.09"];
    private static readonly string[] BradescoEquitySources = ["2.07.01"];

    private readonly TestDatabase _database = new();
    private readonly FixtureZip _zips = new();

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        await _database.AddFixtureCompaniesAsync();
    }

    public async Task DisposeAsync() => await _database.DisposeAsync();

    public void Dispose() => _zips.Dispose();

    private async Task ImportAsync(string folder, string zipName, Func<string, string, IEnumerable<string>>? edit = null, FilingForm form = FilingForm.Dfp)
    {
        await using var db = _database.CreateContext();
        await new StatementImporter(db, NullLogger<StatementImporter>.Instance)
            .ImportAsync(_zips.Create(folder, zipName, edit), form, Downloaded, new HashSet<int> { 9512, 19348, 906, 23159 }, CancellationToken.None);
    }

    private Task Import2024Async(Func<string, string, IEnumerable<string>>? edit = null) => ImportAsync("dfp_2024", Dfp2024, edit);

    private async Task<MetricSeries?> QueryAsync(int cvmCode, Metric metric)
    {
        await using var db = _database.CreateContext();
        return await new MetricQuery(db, NullLogger<MetricQuery>.Instance).GetAnnualAsync(cvmCode, metric, CancellationToken.None);
    }

    private async Task<MetricPoint> SinglePointAsync(int cvmCode, Metric metric) =>
        Assert.Single((await QueryAsync(cvmCode, metric))!.Points);

    [Fact]
    public async Task Petrobras_revenue_comes_from_3_01_with_its_source()
    {
        await Import2024Async();

        var series = (await QueryAsync(Petrobras, Metric.Revenue))!;

        Assert.Equal(AccountingBasis.Consolidated, series.Basis);
        Assert.False(series.NotApplicable);
        var point = Assert.Single(series.Points);
        Assert.Equal(YearEnd2024, point.PeriodEnd);
        Assert.Equal(MetricPointStatus.Value, point.Status);
        Assert.Equal(490_829_000_000m, point.Value);
        Assert.False(point.IsCalculated);
        Assert.Equal(new MetricSource("3.01", new DateOnly(2024, 1, 1), YearEnd2024, Dfp2024, 1, Downloaded), Assert.Single(point.Sources));
    }

    [Fact]
    public async Task Petrobras_equity_is_total_minus_minority_and_says_so()
    {
        // 367,514,000 − 1,508,000 thousand. Equal to Petrobras's individual 2.03,
        // as it should be for a non-bank.
        await Import2024Async();

        var point = await SinglePointAsync(Petrobras, Metric.Equity);

        Assert.Equal(366_006_000_000m, point.Value);
        Assert.True(point.IsCalculated);
        Assert.Equal(PetrobrasEquitySources, point.Sources.Select(s => s.AccountCode));
    }

    [Fact]
    public async Task Petrobras_net_income_is_the_shareholders_part()
    {
        // 36,606,000 + 403,000 = 37,009,000 thousand: the parts add up.
        await Import2024Async();

        var point = await SinglePointAsync(Petrobras, Metric.NetIncome);

        Assert.Equal(36_606_000_000m, point.Value);
        Assert.False(point.IsCalculated);
        Assert.Equal(PetrobrasNetIncomeSources, point.Sources.Select(s => s.AccountCode));
    }

    [Fact]
    public async Task A_bank_has_no_revenue()
    {
        await Import2024Async();

        var series = (await QueryAsync(Itau, Metric.Revenue))!;

        Assert.True(series.NotApplicable);
        var point = Assert.Single(series.Points);
        Assert.Equal(MetricPointStatus.NotApplicable, point.Status);
        Assert.Null(point.Value);
        Assert.Empty(point.Sources);
    }

    [Fact]
    public async Task Itau_equity_is_calculated_from_its_own_layout()
    {
        // Bank309: 221,284,000 − 10,194,000 thousand.
        await Import2024Async();

        var point = await SinglePointAsync(Itau, Metric.Equity);

        Assert.Equal(211_090_000_000m, point.Value);
        Assert.True(point.IsCalculated);
        Assert.Equal(ItauEquitySources, point.Sources.Select(s => s.AccountCode));
    }

    [Fact]
    public async Task Bradesco_equity_is_a_single_published_line()
    {
        // Bank311 consolidated 2.07.01: 168,409,653 (+ 532,839 = 168,942,492 thousand).
        await Import2024Async();

        var point = await SinglePointAsync(Bradesco, Metric.Equity);

        Assert.Equal(168_409_653_000m, point.Value);
        Assert.False(point.IsCalculated);
        Assert.Equal(BradescoEquitySources, point.Sources.Select(s => s.AccountCode));
    }

    [Fact]
    public async Task Bradesco_net_income_reads_the_right_line_before_and_after_its_layout_change()
    {
        // 2017, Bank309, version 2: 3.09.01 = 17,089,364. 2024, Bank311: 3.11.01 = 17,252,900 thousand.
        await ImportAsync("dfp_2017", "dfp_cia_aberta_2017.zip");
        await Import2024Async();

        var points = (await QueryAsync(Bradesco, Metric.NetIncome))!.Points;

        Assert.Equal(2, points.Count);
        Assert.Equal((new DateOnly(2017, 12, 31), 17_089_364_000m, 2), (points[0].PeriodEnd, points[0].Value!.Value, Assert.Single(points[0].Sources).FilingVersion));
        Assert.Equal((YearEnd2024, 17_252_900_000m, 1), (points[1].PeriodEnd, points[1].Value!.Value, Assert.Single(points[1].Sources).FilingVersion));
    }

    [Fact]
    public async Task The_highest_version_wins()
    {
        await Import2024Async();
        await Import2024Async((_, line) =>
            [line.Replace(PetrobrasRow, "33.000.167/0001-01;2024-12-31;2;", StringComparison.Ordinal)
                 .Replace(";490829000.0000000000;", ";491000000.0000000000;", StringComparison.Ordinal)]);

        var point = await SinglePointAsync(Petrobras, Metric.Revenue);

        Assert.Equal(2, Assert.Single(point.Sources).FilingVersion);
        Assert.Equal(491_000_000_000m, point.Value);
    }

    [Fact]
    public async Task Without_consolidated_statements_the_individual_ones_are_used()
    {
        // Individual 2.03 is the shareholders' equity as published: no subtraction.
        await Import2024Async((name, line) =>
            name.Contains("_con_", StringComparison.Ordinal) && line.StartsWith(PetrobrasRow, StringComparison.Ordinal) ? [] : [line]);

        var series = (await QueryAsync(Petrobras, Metric.Equity))!;

        Assert.Equal(AccountingBasis.Individual, series.Basis);
        var point = Assert.Single(series.Points);
        Assert.Equal(366_006_000_000m, point.Value);
        Assert.False(point.IsCalculated);
    }

    [Fact]
    public async Task The_basis_is_chosen_from_quarterly_filings_too()
    {
        // Consolidated ITRs, individual-only DFP: both series must resolve to
        // consolidated, or a Q4 would subtract across bases.
        await Import2024Async((name, line) =>
            name.Contains("_con_", StringComparison.Ordinal) && line.StartsWith(PetrobrasRow, StringComparison.Ordinal) ? [] : [line]);
        await ImportAsync("itr_2024", "itr_cia_aberta_2024.zip", form: FilingForm.Itr);

        var series = (await QueryAsync(Petrobras, Metric.Revenue))!;

        Assert.Equal(AccountingBasis.Consolidated, series.Basis);
        var point = Assert.Single(series.Points);
        Assert.Equal(YearEnd2024, point.PeriodEnd);
        Assert.Equal(MetricPointStatus.Unavailable, point.Status);
        Assert.Null(point.Value);
        Assert.Empty(point.Sources);

        await using var db = _database.CreateContext();
        var quarterly = (await new MetricQuery(db, NullLogger<MetricQuery>.Instance)
            .GetQuarterlyAsync(Petrobras, Metric.Revenue, CancellationToken.None))!;
        Assert.Equal(AccountingBasis.Consolidated, quarterly.Basis);
    }

    [Fact]
    public async Task A_bank_series_with_a_period_on_the_other_basis_only_stays_not_applicable()
    {
        // Bradesco 2017 only on the individual basis, 2024 consolidated.
        await ImportAsync("dfp_2017", "dfp_cia_aberta_2017.zip", (name, line) =>
            name.Contains("_con_", StringComparison.Ordinal) && line.StartsWith("60.746.948/0001-12;", StringComparison.Ordinal) ? [] : [line]);
        await Import2024Async();

        var series = (await QueryAsync(Bradesco, Metric.Revenue))!;

        Assert.True(series.NotApplicable);
        Assert.Equal([MetricPointStatus.Unavailable, MetricPointStatus.NotApplicable], series.Points.Select(p => p.Status));
    }

    [Fact]
    public async Task A_missing_minority_line_makes_equity_unavailable_not_the_total()
    {
        await Import2024Async(FixtureZip.OnRow("dfp_cia_aberta_BPP_con_2024.csv", PetrobrasRow, "2.03.09", _ => []));

        var point = await SinglePointAsync(Petrobras, Metric.Equity);

        Assert.Equal(MetricPointStatus.Unavailable, point.Status);
        Assert.Null(point.Value);
    }

    [Fact]
    public async Task Net_income_with_both_parts_at_zero_is_unavailable()
    {
        await Import2024Async((name, line) =>
            FixtureZip.OnRow(IncomeCon, PetrobrasRow, "3.11.01", l => [l.Replace(";36606000.0000000000;", ";0.0000000000;", StringComparison.Ordinal)])(name, line)
                .SelectMany(l => FixtureZip.OnRow(IncomeCon, PetrobrasRow, "3.11.02", x => [x.Replace(";403000.0000000000;", ";0.0000000000;", StringComparison.Ordinal)])(name, l)));

        Assert.Equal(MetricPointStatus.Unavailable, (await SinglePointAsync(Petrobras, Metric.NetIncome)).Status);
    }

    [Theory]
    [InlineData("403001", MetricPointStatus.Value)]       // parts off by R$1,000: rounding
    [InlineData("403002", MetricPointStatus.Unavailable)] // off by R$2,000: they don't add up
    public async Task Parts_must_add_up_to_the_total_within_a_thousand_reais(string minority, MetricPointStatus expected)
    {
        await Import2024Async(FixtureZip.OnRow(IncomeCon, PetrobrasRow, "3.11.02",
            line => [line.Replace(";403000.0000000000;", $";{minority}.0000000000;", StringComparison.Ordinal)]));

        Assert.Equal(expected, (await SinglePointAsync(Petrobras, Metric.NetIncome)).Status);
    }

    [Fact]
    public async Task A_missing_net_income_total_makes_the_filing_unknown_and_the_point_unavailable()
    {
        // The total of every CheckedPart is itself a layout signal, so through the
        // importer a missing total always means an unknown layout.
        await Import2024Async(FixtureZip.OnRow(IncomeCon, PetrobrasRow, "3.11", _ => []));

        await using var db = _database.CreateContext();
        var filing = await db.Filings.SingleAsync(f => f.CvmCode == Petrobras && f.Basis == AccountingBasis.Consolidated);
        Assert.Equal(StatementLayout.Unknown, filing.Layout);
        Assert.Equal(MetricPointStatus.Unavailable, (await SinglePointAsync(Petrobras, Metric.NetIncome)).Status);
    }

    [Fact]
    public async Task A_filing_in_an_unknown_layout_is_unavailable()
    {
        await Import2024Async((_, line) =>
            [line.Replace(";3.01;Receita de Venda de Bens e/ou Serviços;", ";3.01;Receitas das Operações;", StringComparison.Ordinal)]);

        var series = (await QueryAsync(Petrobras, Metric.TotalAssets))!;

        Assert.False(series.NotApplicable);
        Assert.Equal(MetricPointStatus.Unavailable, Assert.Single(series.Points).Status);
    }

    [Fact]
    public async Task A_conflicting_repeated_row_makes_its_metric_unavailable()
    {
        await Import2024Async(FixtureZip.OnRow(IncomeCon, PetrobrasRow, "3.03",
            line => [line, line.Replace(";246462000.0000000000;", ";1.0000000000;", StringComparison.Ordinal)]));

        Assert.Equal(MetricPointStatus.Unavailable, (await SinglePointAsync(Petrobras, Metric.GrossProfit)).Status);
    }

    [Fact]
    public async Task Quarterly_filings_do_not_make_annual_points()
    {
        await ImportAsync("itr_2024", "itr_cia_aberta_2024.zip", form: FilingForm.Itr);

        Assert.Empty((await QueryAsync(Petrobras, Metric.Revenue))!.Points);

        await Import2024Async();
        Assert.Equal(YearEnd2024, (await SinglePointAsync(Petrobras, Metric.Revenue)).PeriodEnd);
    }

    [Fact]
    public async Task A_known_company_with_nothing_imported_has_an_empty_series()
    {
        var series = (await QueryAsync(Petrobras, Metric.Revenue))!;

        Assert.Empty(series.Points);
        Assert.False(series.NotApplicable);
    }

    [Fact]
    public async Task An_unknown_company_has_no_series()
    {
        Assert.Null(await QueryAsync(4170, Metric.Revenue));
    }
}
