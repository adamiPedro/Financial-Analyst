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

/// <summary>One published row a value was read from.</summary>
public sealed record MetricSource(string AccountCode, string SourceFile, DateTimeOffset RetrievedAt);

/// <summary>
/// One year. Value, Version and Sources are present only when Status is Value.
/// IsCalculated marks a value worked out from two rows rather than published.
/// </summary>
public sealed record MetricPoint(
    DateOnly PeriodEnd,
    MetricPointStatus Status,
    decimal? Value,
    bool IsCalculated,
    int? Version,
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
}
