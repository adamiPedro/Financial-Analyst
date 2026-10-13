namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>Where a metric's value comes from in one filing.</summary>
public abstract record MetricRule;

/// <summary>
/// The layout and basis pair is not in the mapping, Unknown included. Served as
/// unavailable: we cannot tell which row would be right.
/// </summary>
public sealed record NotSupported : MetricRule;

/// <summary>The layout has no such figure, e.g. a bank's gross profit.</summary>
public sealed record NotApplicable : MetricRule;

/// <summary>One published row.</summary>
public sealed record Account(string Code) : MetricRule;

/// <summary>
/// Total minus the minority owners' line, for layouts that publish no line for
/// the shareholders' part alone. The result is calculated, not published.
/// </summary>
public sealed record TotalMinus(string Total, string Minority) : MetricRule;

/// <summary>
/// The published shareholders' line, served only when it and the minority line
/// add up to the total. From 2021 about a hundred filers a year leave both
/// parts at zero with the profit only in the total, and a few file the part
/// with the wrong sign; read alone, those come out as a confident wrong number.
/// </summary>
public sealed record CheckedPart(string Part, string Minority, string Total) : MetricRule;
