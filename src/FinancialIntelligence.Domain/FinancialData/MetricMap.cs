namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// Which rows a metric comes from, per layout and basis. Keyed by basis too,
/// because the same code means different things on each: Bradesco's
/// consolidated 2.07.01 is shareholders' equity, its individual 2.07.01 is
/// paid-in capital. See docs/decisions/0009.
/// </summary>
public static class MetricMap
{
    private static readonly NotSupported Unsupported = new();
    private static readonly NotApplicable None = new();

    public static MetricRule Find(StatementLayout layout, AccountingBasis basis, Metric metric) =>
        basis == AccountingBasis.Consolidated ? Consolidated(layout, metric) : Individual(layout, metric);

    // Banks have no revenue, gross profit or operating cash flow that compares
    // with an industrial's: interest income is not sales, and a bank's operating
    // cash moves with its deposits. Not applicable is the honest answer.
    private static MetricRule Consolidated(StatementLayout layout, Metric metric) => layout switch
    {
        StatementLayout.Industrial => metric switch
        {
            Metric.Revenue => new Account("3.01"),
            Metric.GrossProfit => new Account("3.03"),
            Metric.NetIncome => new CheckedPart("3.11.01", "3.11.02", "3.11"),
            Metric.OperatingCashFlow => new Account("6.01"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new TotalMinus("2.03", "2.03.09"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Bank309 => metric switch
        {
            Metric.Revenue or Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new CheckedPart("3.09.01", "3.09.02", "3.09"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new TotalMinus("2.08", "2.08.09"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Bank311 => metric switch
        {
            Metric.Revenue or Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new CheckedPart("3.11.01", "3.11.02", "3.11"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new CheckedPart("2.07.01", "2.07.02", "2.07"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Insurer => metric switch
        {
            Metric.Revenue => new Account("3.01"),
            Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new CheckedPart("3.13.01", "3.13.02", "3.13"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new TotalMinus("2.03", "2.03.09"),
            _ => throw Unknown(metric)
        },
        _ => Unsupported
    };

    // Individual statements cover the parent alone, which has no minority
    // owners, so the totals are already the shareholders' part.
    private static MetricRule Individual(StatementLayout layout, Metric metric) => layout switch
    {
        StatementLayout.Industrial => metric switch
        {
            Metric.Revenue => new Account("3.01"),
            Metric.GrossProfit => new Account("3.03"),
            Metric.NetIncome => new Account("3.11"),
            Metric.OperatingCashFlow => new Account("6.01"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new Account("2.03"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Bank311 => metric switch
        {
            Metric.Revenue or Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new Account("3.11"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new Account("2.07"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Bank313 => metric switch
        {
            Metric.Revenue or Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new Account("3.13"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new Account("2.05"),
            _ => throw Unknown(metric)
        },
        StatementLayout.Insurer => metric switch
        {
            Metric.Revenue => new Account("3.01"),
            Metric.GrossProfit or Metric.OperatingCashFlow => None,
            Metric.NetIncome => new Account("3.13"),
            Metric.TotalAssets => new Account("1"),
            Metric.Equity => new Account("2.03"),
            _ => throw Unknown(metric)
        },
        _ => Unsupported
    };

    private static ArgumentOutOfRangeException Unknown(Metric metric) =>
        new(nameof(metric), metric, "Metric has no mapping.");
}
