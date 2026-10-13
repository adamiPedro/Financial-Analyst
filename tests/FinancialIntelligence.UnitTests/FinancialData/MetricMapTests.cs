using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.UnitTests.FinancialData;

// Each cell was checked by hand against named real filings (2024 DFP):
// Industrial = Petrobras, Bank309 = Itaú consolidated, Bank311 = Bradesco,
// Bank313 = Itaú individual, Insurer = BB Seguridade. See the spec's tables.
public class MetricMapTests
{
    private static readonly NotApplicable NA = new();

    public static TheoryData<StatementLayout, Metric, MetricRule> Consolidated => new()
    {
        { StatementLayout.Industrial, Metric.Revenue, new Account("3.01") },
        { StatementLayout.Industrial, Metric.GrossProfit, new Account("3.03") },
        { StatementLayout.Industrial, Metric.NetIncome, new CheckedPart("3.11.01", "3.11.02", "3.11") },
        { StatementLayout.Industrial, Metric.OperatingCashFlow, new Account("6.01") },
        { StatementLayout.Industrial, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Industrial, Metric.Equity, new TotalMinus("2.03", "2.03.09") },

        { StatementLayout.Bank309, Metric.Revenue, NA },
        { StatementLayout.Bank309, Metric.GrossProfit, NA },
        { StatementLayout.Bank309, Metric.NetIncome, new CheckedPart("3.09.01", "3.09.02", "3.09") },
        { StatementLayout.Bank309, Metric.OperatingCashFlow, NA },
        { StatementLayout.Bank309, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Bank309, Metric.Equity, new TotalMinus("2.08", "2.08.09") },

        { StatementLayout.Bank311, Metric.Revenue, NA },
        { StatementLayout.Bank311, Metric.GrossProfit, NA },
        { StatementLayout.Bank311, Metric.NetIncome, new CheckedPart("3.11.01", "3.11.02", "3.11") },
        { StatementLayout.Bank311, Metric.OperatingCashFlow, NA },
        { StatementLayout.Bank311, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Bank311, Metric.Equity, new CheckedPart("2.07.01", "2.07.02", "2.07") },

        { StatementLayout.Insurer, Metric.Revenue, new Account("3.01") },
        { StatementLayout.Insurer, Metric.GrossProfit, NA },
        { StatementLayout.Insurer, Metric.NetIncome, new CheckedPart("3.13.01", "3.13.02", "3.13") },
        { StatementLayout.Insurer, Metric.OperatingCashFlow, NA },
        { StatementLayout.Insurer, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Insurer, Metric.Equity, new TotalMinus("2.03", "2.03.09") },
    };

    public static TheoryData<StatementLayout, Metric, MetricRule> Individual => new()
    {
        { StatementLayout.Industrial, Metric.Revenue, new Account("3.01") },
        { StatementLayout.Industrial, Metric.GrossProfit, new Account("3.03") },
        { StatementLayout.Industrial, Metric.NetIncome, new Account("3.11") },
        { StatementLayout.Industrial, Metric.OperatingCashFlow, new Account("6.01") },
        { StatementLayout.Industrial, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Industrial, Metric.Equity, new Account("2.03") },

        { StatementLayout.Bank311, Metric.Revenue, NA },
        { StatementLayout.Bank311, Metric.GrossProfit, NA },
        { StatementLayout.Bank311, Metric.NetIncome, new Account("3.11") },
        { StatementLayout.Bank311, Metric.OperatingCashFlow, NA },
        { StatementLayout.Bank311, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Bank311, Metric.Equity, new Account("2.07") },

        { StatementLayout.Bank313, Metric.Revenue, NA },
        { StatementLayout.Bank313, Metric.GrossProfit, NA },
        { StatementLayout.Bank313, Metric.NetIncome, new Account("3.13") },
        { StatementLayout.Bank313, Metric.OperatingCashFlow, NA },
        { StatementLayout.Bank313, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Bank313, Metric.Equity, new Account("2.05") },

        { StatementLayout.Insurer, Metric.Revenue, new Account("3.01") },
        { StatementLayout.Insurer, Metric.GrossProfit, NA },
        { StatementLayout.Insurer, Metric.NetIncome, new Account("3.13") },
        { StatementLayout.Insurer, Metric.OperatingCashFlow, NA },
        { StatementLayout.Insurer, Metric.TotalAssets, new Account("1") },
        { StatementLayout.Insurer, Metric.Equity, new Account("2.03") },
    };

    [Theory]
    [MemberData(nameof(Consolidated))]
    public void Consolidated_cells(StatementLayout layout, Metric metric, MetricRule expected)
    {
        Assert.Equal(expected, MetricMap.Find(layout, AccountingBasis.Consolidated, metric));
    }

    [Theory]
    [MemberData(nameof(Individual))]
    public void Individual_cells(StatementLayout layout, Metric metric, MetricRule expected)
    {
        Assert.Equal(expected, MetricMap.Find(layout, AccountingBasis.Individual, metric));
    }

    [Theory]
    [InlineData(StatementLayout.Unknown, AccountingBasis.Consolidated)]
    [InlineData(StatementLayout.Unknown, AccountingBasis.Individual)]
    [InlineData(StatementLayout.Bank309, AccountingBasis.Individual)]
    [InlineData(StatementLayout.Bank313, AccountingBasis.Consolidated)]
    public void Pairs_not_in_the_tables_are_not_supported(StatementLayout layout, AccountingBasis basis)
    {
        foreach (var metric in Enum.GetValues<Metric>())
        {
            Assert.Equal(new NotSupported(), MetricMap.Find(layout, basis, metric));
        }
    }

    [Fact]
    public void Every_metric_is_in_both_tables()
    {
        Assert.Equal(Enum.GetValues<Metric>().Length * 4, Consolidated.Count);
        Assert.Equal(Enum.GetValues<Metric>().Length * 4, Individual.Count);
    }

    [Theory]
    [InlineData(Metric.Revenue, MetricKind.Flow)]
    [InlineData(Metric.GrossProfit, MetricKind.Flow)]
    [InlineData(Metric.NetIncome, MetricKind.Flow)]
    [InlineData(Metric.OperatingCashFlow, MetricKind.Flow)]
    [InlineData(Metric.TotalAssets, MetricKind.Balance)]
    [InlineData(Metric.Equity, MetricKind.Balance)]
    public void Each_metric_is_a_flow_or_a_balance(Metric metric, MetricKind kind)
    {
        Assert.Equal(kind, metric.Kind());
    }
}
