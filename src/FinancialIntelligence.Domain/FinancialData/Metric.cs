namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// The comparable figures served per company. NetIncome and Equity are the
/// parent shareholders' part, because that is how return on equity is quoted.
/// </summary>
public enum Metric
{
    Revenue = 1,
    GrossProfit = 2,
    NetIncome = 3,
    OperatingCashFlow = 4,
    TotalAssets = 5,
    Equity = 6
}

/// <summary>
/// A flow covers a period and a balance is a position on a date. The fourth
/// quarter is derived differently for each: annual minus the nine-month
/// year-to-date for a flow, the year-end figure itself for a balance.
/// </summary>
public enum MetricKind
{
    Flow = 1,
    Balance = 2
}

public static class MetricExtensions
{
    public static MetricKind Kind(this Metric metric) => metric switch
    {
        Metric.Revenue or Metric.GrossProfit or Metric.NetIncome or Metric.OperatingCashFlow => MetricKind.Flow,
        Metric.TotalAssets or Metric.Equity => MetricKind.Balance,
        _ => throw new ArgumentOutOfRangeException(nameof(metric), metric, null)
    };
}
