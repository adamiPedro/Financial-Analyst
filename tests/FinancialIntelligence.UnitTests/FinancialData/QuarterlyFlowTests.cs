using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.UnitTests.FinancialData;

// Figures are Petrobras's consolidated 2024 filings, in thousands as published
// (itr_cia_aberta_2024.zip and dfp_cia_aberta_2024.zip, rows ÚLTIMO; see
// tests/Fixtures/Cvm). The expected quarters were worked out by hand.
public class QuarterlyFlowTests
{
    private static readonly DateOnly Jan1 = new(2024, 1, 1);
    private static readonly DateOnly Mar31 = new(2024, 3, 31);
    private static readonly DateOnly Jun30 = new(2024, 6, 30);
    private static readonly DateOnly Sep30 = new(2024, 9, 30);
    private static readonly DateOnly Dec31 = new(2024, 12, 31);

    private static readonly Filing Source = new(
        9512, FilingForm.Itr, Sep30, 1, AccountingBasis.Consolidated, StatementLayout.Industrial,
        "itr_cia_aberta_2024.zip", DateTimeOffset.UnixEpoch);

    private static Reading Row(string code, DateOnly start, DateOnly end, decimal thousands) =>
        new(thousands * 1000, false, [new FinancialFact(Source, code, start, end, thousands * 1000)]);

    private static decimal?[] Values(IReadOnlyList<Reading?> quarters) => [.. quarters.Select(q => q?.Value)];

    [Fact]
    public void Petrobras_2024_revenue_q4_is_the_annual_figure_minus_nine_months()
    {
        // Q4 = 490,829,000 − 369,561,000 = 121,268,000.
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Mar31, Row("3.01", Jan1, Mar31, 117_721_000), Row("3.01", Jan1, Mar31, 117_721_000)),
            new(Jan1, Jun30, Row("3.01", new(2024, 4, 1), Jun30, 122_258_000), Row("3.01", Jan1, Jun30, 239_979_000)),
            new(Jan1, Sep30, Row("3.01", new(2024, 7, 1), Sep30, 129_582_000), Row("3.01", Jan1, Sep30, 369_561_000)),
            new(Jan1, Dec31, null, Row("3.01", Jan1, Dec31, 490_829_000)),
        ]);

        Assert.Equal([117_721_000_000m, 122_258_000_000m, 129_582_000_000m, 121_268_000_000m], Values(quarters));
        Assert.Equal([false, false, false, true], quarters.Select(q => q!.IsCalculated));
        Assert.Equal([Dec31, Sep30], quarters[3]!.Sources.Select(f => f.PeriodEnd));
    }

    [Fact]
    public void Petrobras_2024_cash_flow_q2_to_q4_come_from_year_to_date_differences()
    {
        // The quarterly cash flow has no three-month rows after Q1.
        // Q2 = 93,651,000 − 46,481,000; Q3 = 156,371,000 − 93,651,000; Q4 = 204,037,000 − 156,371,000.
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Mar31, Row("6.01", Jan1, Mar31, 46_481_000), Row("6.01", Jan1, Mar31, 46_481_000)),
            new(Jan1, Jun30, null, Row("6.01", Jan1, Jun30, 93_651_000)),
            new(Jan1, Sep30, null, Row("6.01", Jan1, Sep30, 156_371_000)),
            new(Jan1, Dec31, null, Row("6.01", Jan1, Dec31, 204_037_000)),
        ]);

        Assert.Equal([46_481_000_000m, 47_170_000_000m, 62_720_000_000m, 47_666_000_000m], Values(quarters));
        Assert.Equal([false, true, true, true], quarters.Select(q => q!.IsCalculated));
    }

    [Fact]
    public void A_loss_in_the_fourth_quarter_comes_out_negative()
    {
        // Net income to shareholders (3.11.01): 36,606,000 for the year, 53,650,000 to September.
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Sep30, Row("3.11.01", new(2024, 7, 1), Sep30, 32_555_000), Row("3.11.01", Jan1, Sep30, 53_650_000)),
            new(Jan1, Dec31, null, Row("3.11.01", Jan1, Dec31, 36_606_000)),
        ]);

        Assert.Equal(-17_044_000_000m, quarters[1]!.Value);
    }

    [Fact]
    public void Without_the_nine_month_figure_q4_is_unavailable()
    {
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Sep30, Row("3.01", new(2024, 7, 1), Sep30, 129_582_000), null),
            new(Jan1, Dec31, null, Row("3.01", Jan1, Dec31, 490_829_000)),
        ]);

        Assert.Null(quarters[1]);
    }

    [Fact]
    public void A_missing_previous_quarter_makes_the_difference_unavailable()
    {
        // No Q2 filing: 9m − 3m would be six months, not Q3.
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Mar31, Row("6.01", Jan1, Mar31, 46_481_000), Row("6.01", Jan1, Mar31, 46_481_000)),
            new(Jan1, Sep30, null, Row("6.01", Jan1, Sep30, 156_371_000)),
            new(Jan1, Dec31, null, Row("6.01", Jan1, Dec31, 204_037_000)),
        ]);

        Assert.Equal([46_481_000_000m, null, 47_666_000_000m], Values(quarters));
    }

    [Fact]
    public void Year_to_date_is_never_subtracted_across_years()
    {
        var jan1Next = new DateOnly(2025, 1, 1);
        var mar31Next = new DateOnly(2025, 3, 31);

        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Dec31, null, Row("3.01", Jan1, Dec31, 490_829_000)),
            new(jan1Next, mar31Next, null, Row("3.01", jan1Next, mar31Next, 120_000_000)),
        ]);

        Assert.Null(quarters[1]);
    }

    [Fact]
    public void A_period_with_no_rows_is_unavailable_and_breaks_the_chain()
    {
        var quarters = QuarterlyFlow.Derive(
        [
            new(Jan1, Jun30, null, Row("6.01", Jan1, Jun30, 93_651_000)),
            new(null, Sep30, null, null),
            new(Jan1, Dec31, null, Row("6.01", Jan1, Dec31, 204_037_000)),
        ]);

        Assert.Equal([null, null, null], Values(quarters));
    }
}
