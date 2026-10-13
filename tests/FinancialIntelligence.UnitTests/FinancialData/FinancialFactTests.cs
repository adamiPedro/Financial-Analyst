using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.UnitTests.FinancialData;

public class FinancialFactTests
{
    private static readonly DateTimeOffset Retrieved = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    private static readonly Filing Filing = new(9512, FilingForm.Dfp, new DateOnly(2024, 12, 31), 2,
        AccountingBasis.Individual, StatementLayout.Industrial, "dfp_cia_aberta_2024.zip", Retrieved);

    [Fact]
    public void A_fact_takes_its_company_basis_version_and_source_from_its_filing()
    {
        var fact = new FinancialFact(Filing, "3.01", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 473_547_000_000m);

        Assert.Same(Filing, fact.Filing);
        Assert.Equal(9512, fact.CvmCode);
        Assert.Equal(AccountingBasis.Individual, fact.Basis);
        Assert.Equal(2, fact.Version);
        Assert.Equal("dfp_cia_aberta_2024.zip", fact.SourceFile);
        Assert.Equal(Retrieved, fact.RetrievedAt);
        Assert.Equal("3.01", fact.AccountCode);
        Assert.Equal(473_547_000_000m, fact.Value);
    }

    [Fact]
    public void A_balance_has_no_start_date()
    {
        var fact = new FinancialFact(Filing, "1", null, new DateOnly(2024, 12, 31), 1_569_110_000_000m);

        Assert.Null(fact.PeriodStart);
        Assert.Equal(new DateOnly(2024, 12, 31), fact.PeriodEnd);
    }

    [Fact]
    public void A_period_cannot_end_before_it_starts()
    {
        Assert.Throws<ArgumentException>(() =>
            new FinancialFact(Filing, "3.01", new DateOnly(2024, 12, 31), new DateOnly(2024, 1, 1), 1m));
    }

    [Fact]
    public void Account_code_is_required()
    {
        Assert.Throws<ArgumentException>(() =>
            new FinancialFact(Filing, "", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 1m));
    }

    [Fact]
    public void Filing_is_required()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new FinancialFact(null!, "3.01", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31), 1m));
    }
}
