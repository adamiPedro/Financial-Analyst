using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Application.FinancialData;

public enum MetricPointStatus
{
    Value = 1,

    /// <summary>The metric applies, but this filing gives no trustworthy value for it.</summary>
    Unavailable = 2,

    /// <summary>The filing's layout has no such figure, e.g. a bank's revenue.</summary>
    NotApplicable = 3
}

/// <summary>
/// One published row a value was read from, and the filing version that published it.
/// The period is there because a calculated point's rows can share account and file:
/// it says which period each covers. PeriodStart is null for a balance.
/// </summary>
public sealed record MetricSource(
    string AccountCode, DateOnly? PeriodStart, DateOnly PeriodEnd, string SourceFile, int FilingVersion, DateTimeOffset RetrievedAt);

/// <summary>
/// One period. Value and Sources are present only when Status is Value.
/// IsCalculated marks a value worked out from several rows rather than published;
/// its sources can come from different filings, each with its own version.
/// </summary>
public sealed record MetricPoint(
    DateOnly PeriodEnd,
    MetricPointStatus Status,
    decimal? Value,
    bool IsCalculated,
    IReadOnlyList<MetricSource> Sources);

/// <summary>NotApplicable is true when every point is, e.g. revenue for a bank.</summary>
public sealed record MetricSeries(
    Metric Metric,
    AccountingBasis Basis,
    bool NotApplicable,
    IReadOnlyList<MetricPoint> Points);

public interface IMetricQuery
{
    /// <summary>
    /// One point per annual filing, oldest first, latest version of each, all on
    /// one basis. Null when the company is unknown; empty when nothing is imported.
    /// </summary>
    Task<MetricSeries?> GetAnnualAsync(int cvmCode, Metric metric, CancellationToken cancellationToken);

    /// <summary>
    /// One point per quarter, oldest first, on the same basis as the annual
    /// series. Q4 of a flow is calculated, as are cash-flow Q2 and Q3.
    /// </summary>
    Task<MetricSeries?> GetQuarterlyAsync(int cvmCode, Metric metric, CancellationToken cancellationToken);
}
