using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Cvm;

namespace FinancialIntelligence.UnitTests.Cvm;

// Expected values are read by hand from the fixture, which is a trimmed copy of
// dfp_cia_aberta_DRE_con_2024.csv (see Fixtures/Cvm/README.md).
public class DfpStatementReaderTests
{
    private const int Petrobras = 9512;
    private const int Vivara = 24805;

    private static async Task<IReadOnlyList<StatementValue>> Read(int cvmCode, string accountCode)
    {
        await using var file = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", "dfp_cia_aberta_DRE_con_2024.csv"));
        return await DfpStatementReader.ReadAccountAsync(file, cvmCode, accountCode, CancellationToken.None);
    }

    [Fact]
    public async Task Petrobras_2024_revenue_is_read_once_and_scaled_from_thousands()
    {
        // File: 490829000 in MIL for 2024 (ÚLTIMO), plus 511994000 for the 2023
        // comparative (PENÚLTIMO), which must not come back.
        var value = Assert.Single(await Read(Petrobras, "3.01"));

        Assert.Equal(490_829_000_000m, value.Value);
        Assert.Equal(new DateOnly(2024, 1, 1), value.PeriodStart);
        Assert.Equal(new DateOnly(2024, 12, 31), value.PeriodEnd);
        Assert.Equal(AccountingBasis.Consolidated, value.Basis);
        Assert.Equal(1, value.Version);
        Assert.Equal(Petrobras, value.CvmCode);
    }

    [Fact]
    public async Task Vivara_revenue_published_in_reais_is_not_scaled()
    {
        var value = Assert.Single(await Read(Vivara, "3.01"));

        Assert.Equal(2_577_113_417m, value.Value);
    }

    [Fact]
    public async Task Earnings_per_share_is_not_scaled_even_though_the_row_says_thousands()
    {
        // Petrobras 2024 basic EPS, ON shares: R$ 2.84, labelled MIL in the file.
        var value = Assert.Single(await Read(Petrobras, "3.99.01.01"));

        Assert.Equal(2.84m, value.Value);
    }

    [Fact]
    public async Task A_company_not_in_the_file_returns_nothing()
    {
        Assert.Empty(await Read(4170, "3.01"));
    }
}
