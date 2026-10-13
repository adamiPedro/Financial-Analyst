using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.UnitTests.FinancialData;

public class FilingTests
{
    private static readonly DateTimeOffset Retrieved = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);

    private static Filing Petrobras2024(int version = 1, string sourceFile = "dfp_cia_aberta_2024.zip") =>
        new(9512, FilingForm.Dfp, new DateOnly(2024, 12, 31), version, AccountingBasis.Consolidated,
            StatementLayout.Industrial, sourceFile, Retrieved);

    [Fact]
    public void A_filing_keeps_what_it_was_given()
    {
        var filing = Petrobras2024();

        Assert.Equal(9512, filing.CvmCode);
        Assert.Equal(FilingForm.Dfp, filing.Form);
        Assert.Equal(new DateOnly(2024, 12, 31), filing.ReferenceDate);
        Assert.Equal(1, filing.Version);
        Assert.Equal(AccountingBasis.Consolidated, filing.Basis);
        Assert.Equal(StatementLayout.Industrial, filing.Layout);
        Assert.Equal("dfp_cia_aberta_2024.zip", filing.SourceFile);
        Assert.Equal(Retrieved, filing.RetrievedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Version_must_be_positive(int version)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Petrobras2024(version: version));
    }

    [Fact]
    public void Source_file_is_required()
    {
        Assert.Throws<ArgumentException>(() => Petrobras2024(sourceFile: " "));
    }

    [Fact]
    public void Cvm_code_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Filing(0, FilingForm.Dfp, new DateOnly(2024, 12, 31), 1,
            AccountingBasis.Consolidated, StatementLayout.Industrial, "dfp_cia_aberta_2024.zip", Retrieved));
    }

    [Fact]
    public void The_layout_can_be_corrected()
    {
        var filing = Petrobras2024();

        filing.ChangeLayout(StatementLayout.Unknown);

        Assert.Equal(StatementLayout.Unknown, filing.Layout);
    }
}
