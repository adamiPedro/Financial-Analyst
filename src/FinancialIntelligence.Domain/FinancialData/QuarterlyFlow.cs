namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// One filing's figures for a flow metric. ThreeMonths is null where nothing
/// three months long is published: Q2 and Q3 cash flow, and every DFP.
/// YearStart is null when the filing has no rows for the metric.
/// </summary>
public sealed record FlowPeriod(DateOnly? YearStart, DateOnly PeriodEnd, Reading? ThreeMonths, Reading? YearToDate);

/// <summary>
/// There is no fourth-quarter ITR, and the quarterly cash flow is published
/// only year to date. A quarter that is not published is this period's
/// year-to-date minus the previous quarter's.
/// </summary>
public static class QuarterlyFlow
{
    /// <param name="periods">Oldest first. Result is one entry per period, null when unavailable.</param>
    public static IReadOnlyList<Reading?> Derive(IReadOnlyList<FlowPeriod> periods)
    {
        var quarters = new List<Reading?>(periods.Count);
        FlowPeriod? previous = null;

        foreach (var period in periods)
        {
            quarters.Add(period.ThreeMonths ?? Difference(period, previous));
            previous = period;
        }

        return quarters;
    }

    // Only the quarter immediately before, in the same year: with Q2 missing,
    // nine months minus three would be served as Q3 while it covers six.
    private static Reading? Difference(FlowPeriod period, FlowPeriod? previous) =>
        period is { YearStart: { } yearStart, YearToDate: { } toDate }
        && previous is { YearToDate: { } before }
        && previous.YearStart == yearStart
        && previous.PeriodEnd > period.PeriodEnd.AddMonths(-4)
            ? new Reading(toDate.Value - before.Value, IsCalculated: true, [.. toDate.Sources, .. before.Sources])
            : null;
}
