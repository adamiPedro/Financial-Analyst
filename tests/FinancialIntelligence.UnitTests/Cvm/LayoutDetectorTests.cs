using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Cvm;

namespace FinancialIntelligence.UnitTests.Cvm;

// Layouts from real filings in the fixture folders (see Fixtures/Cvm/README.md).
public class LayoutDetectorTests
{
    private static async Task<List<StatementRow>> ReadFolder(string folder, int cvmCode)
    {
        var rows = new List<StatementRow>();
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", folder)))
        {
            await using var csv = File.OpenRead(file);
            rows.AddRange(await StatementReader.ReadAsync(csv, new HashSet<int> { cvmCode }, CancellationToken.None));
        }

        return rows;
    }

    private static async Task<List<StatementRow>> Filing(string folder, int cvmCode, AccountingBasis basis) =>
        (await ReadFolder(folder, cvmCode)).Where(r => r.Basis == basis).ToList();

    [Theory]
    [InlineData("dfp_2024", 9512, AccountingBasis.Consolidated, StatementLayout.Industrial)]
    [InlineData("dfp_2024", 9512, AccountingBasis.Individual, StatementLayout.Industrial)]
    [InlineData("dfp_2024", 19348, AccountingBasis.Consolidated, StatementLayout.Bank309)]
    [InlineData("dfp_2024", 19348, AccountingBasis.Individual, StatementLayout.Bank313)]
    [InlineData("dfp_2024", 906, AccountingBasis.Consolidated, StatementLayout.Bank311)]
    [InlineData("dfp_2024", 906, AccountingBasis.Individual, StatementLayout.Bank311)]
    [InlineData("dfp_2024", 23159, AccountingBasis.Consolidated, StatementLayout.Insurer)]
    [InlineData("dfp_2024", 23159, AccountingBasis.Individual, StatementLayout.Insurer)]
    [InlineData("dfp_2017", 906, AccountingBasis.Consolidated, StatementLayout.Bank309)]
    [InlineData("dfp_2017", 906, AccountingBasis.Individual, StatementLayout.Bank313)]
    public async Task Real_filings_are_detected(string folder, int cvmCode, AccountingBasis basis, StatementLayout expected)
    {
        Assert.Equal(expected, LayoutDetector.Detect(await Filing(folder, cvmCode, basis)));
    }

    [Fact]
    public async Task Quarterly_filings_use_the_same_layouts()
    {
        var quarters = (await ReadFolder("itr_2024", 9512)).GroupBy(r => (r.ReferenceDate, r.Basis)).ToList();

        Assert.Equal(6, quarters.Count);
        Assert.All(quarters, q => Assert.Equal(StatementLayout.Industrial, LayoutDetector.Detect(q.ToList())));
    }

    [Fact]
    public async Task Signals_that_match_no_layout_give_unknown()
    {
        // Bank-style 3.01 with an industrial's net income and equity lines.
        var rows = (await Filing("dfp_2024", 9512, AccountingBasis.Consolidated))
            .Select(r => r.AccountCode == "3.01" ? r with { AccountName = "Receitas da Intermediação Financeira" } : r)
            .ToList();

        Assert.Equal(StatementLayout.Unknown, LayoutDetector.Detect(rows));
    }

    [Fact]
    public async Task Insurers_before_2023_wording_give_unknown()
    {
        // Pre-2023 insurers call 3.01 "Receitas das Operações". Accepted in the spec.
        var rows = (await Filing("dfp_2024", 23159, AccountingBasis.Consolidated))
            .Select(r => r.AccountCode == "3.01" ? r with { AccountName = "Receitas das Operações" } : r)
            .ToList();

        Assert.Equal(StatementLayout.Unknown, LayoutDetector.Detect(rows));
    }

    [Fact]
    public async Task A_filing_without_3_01_is_unknown()
    {
        var rows = (await Filing("dfp_2024", 9512, AccountingBasis.Consolidated))
            .Where(r => r.AccountCode != "3.01")
            .ToList();

        Assert.Equal(StatementLayout.Unknown, LayoutDetector.Detect(rows));
    }

    [Fact]
    public async Task A_filing_without_an_equity_line_is_unknown()
    {
        var rows = (await Filing("dfp_2024", 9512, AccountingBasis.Consolidated))
            .Where(r => r.AccountCode != "2.03")
            .ToList();

        Assert.Equal(StatementLayout.Unknown, LayoutDetector.Detect(rows));
    }
}
