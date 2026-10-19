using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinancialIntelligence.Infrastructure.FinancialData;

internal sealed partial class MetricQuery(FinancialIntelligenceDbContext db, ILogger<MetricQuery> logger) : IMetricQuery
{
    // One unit of the MIL scale most filers publish in, so their own rounding
    // never makes a good filing look broken.
    private const decimal AddUpTolerance = 1000m;

    private static readonly IReadOnlyList<MetricSource> NoSources = [];

    private sealed record Period(FilingForm Form, DateOnly ReferenceDate, Filing? Filing);

    public async Task<MetricSeries?> GetAnnualAsync(int cvmCode, Metric metric, CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.CvmCode == cvmCode, cancellationToken))
        {
            return null;
        }

        var (basis, latest) = await LatestFilingsAsync(cvmCode, cancellationToken);
        var annual = latest.Where(p => p.Form == FilingForm.Dfp).ToList();
        var rules = RulesFor(annual, metric);
        var facts = await FactsAsync(rules, cancellationToken);

        return Series(metric, basis, annual, annual.Select(p => PointFor(p, rules, facts)).ToList());
    }

    public async Task<MetricSeries?> GetQuarterlyAsync(int cvmCode, Metric metric, CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.CvmCode == cvmCode, cancellationToken))
        {
            return null;
        }

        var (basis, latest) = await LatestFilingsAsync(cvmCode, cancellationToken);
        var rules = RulesFor(latest, metric);
        var facts = await FactsAsync(rules, cancellationToken);

        // A balance is a position on a date: each filing's own figure, the
        // DFP's year-end one included, is the quarter.
        if (metric.Kind() == MetricKind.Balance)
        {
            return Series(metric, basis, latest, latest.Select(p => PointFor(p, rules, facts)).ToList());
        }

        // A gap still goes into the chain: Derive must not subtract across it.
        var flows = latest
            .Select(p => p.Filing is { } filing
                ? FlowPeriodOf(filing, rules[filing.Id], facts[filing.Id].ToList())
                : new FlowPeriod(null, p.ReferenceDate, null, null))
            .ToList();
        var quarters = QuarterlyFlow.Derive(flows);

        return Series(metric, basis, latest, latest
            .Select((p, i) => p.Filing is not { } filing ? MissingPoint(p.ReferenceDate)
                : rules[filing.Id] is NotApplicable ? NotApplicablePoint(p.ReferenceDate)
                : ToPoint(p.ReferenceDate, quarters[i]))
            .ToList());
    }

    private static Dictionary<long, MetricRule> RulesFor(List<Period> periods, Metric metric) =>
        periods.Where(p => p.Filing is not null)
            .ToDictionary(p => p.Filing!.Id, p => MetricMap.Find(p.Filing!.Layout, p.Filing.Basis, metric));

    // An ITR states Q2 and Q3 income twice, three months and year to date, on
    // the same end date. The year-to-date row starts with the year; a
    // three-month row starts inside the last three months. In Q1 they are one row.
    private FlowPeriod FlowPeriodOf(Filing filing, MetricRule rule, List<FinancialFact> facts)
    {
        if (rule is NotApplicable || facts.Min(f => f.PeriodStart) is not { } yearStart)
        {
            return new FlowPeriod(null, filing.ReferenceDate, null, null);
        }

        var lastThreeMonths = filing.ReferenceDate.AddMonths(-3);

        return new FlowPeriod(
            yearStart,
            filing.ReferenceDate,
            Read(filing, rule, facts.Where(f => f.PeriodStart > lastThreeMonths).ToList()),
            Read(filing, rule, facts.Where(f => f.PeriodStart == yearStart).ToList()));
    }

    private async Task<(AccountingBasis Basis, List<Period> Latest)> LatestFilingsAsync(int cvmCode, CancellationToken cancellationToken)
    {
        var filings = await db.Filings
            .AsNoTracking()
            .Where(f => f.CvmCode == cvmCode)
            .ToListAsync(cancellationToken);

        // ADR 0008: one basis per company, chosen across annual and quarterly
        // filings, so a fourth quarter can never subtract across bases.
        var basis = filings.Any(f => f.Basis == AccountingBasis.Consolidated)
            ? AccountingBasis.Consolidated
            : AccountingBasis.Individual;

        // Periods are taken from both bases so one filed only on the other
        // basis stays in the series, with a null Filing.
        var latest = filings
            .GroupBy(f => (f.Form, f.ReferenceDate))
            .Select(period => new Period(
                period.Key.Form,
                period.Key.ReferenceDate,
                period.Where(f => f.Basis == basis).MaxBy(f => f.Version)))
            .OrderBy(p => p.ReferenceDate)
            .ToList();

        return (basis, latest);
    }

    // Each filing can need different accounts: the layout decides, per filing.
    private async Task<ILookup<long, FinancialFact>> FactsAsync(Dictionary<long, MetricRule> rules, CancellationToken cancellationToken)
    {
        var filingIds = rules.Keys.ToList();
        var codes = rules.Values.SelectMany(AccountsOf).Distinct().ToList();

        return (await db.FinancialFacts
                .AsNoTracking()
                .Where(f => filingIds.Contains(f.FilingId) && codes.Contains(f.AccountCode))
                .ToListAsync(cancellationToken))
            .ToLookup(f => f.FilingId);
    }

    // Placeholders don't count towards NotApplicable: a bank with a gap in its
    // filings still has no revenue.
    private static MetricSeries Series(Metric metric, AccountingBasis basis, List<Period> periods, List<MetricPoint> points)
    {
        var filed = points.Where((_, i) => periods[i].Filing is not null).ToList();

        return new(metric, basis, filed.Count > 0 && filed.All(p => p.Status == MetricPointStatus.NotApplicable), points);
    }

    private MetricPoint PointFor(Period period, Dictionary<long, MetricRule> rules, ILookup<long, FinancialFact> facts)
    {
        if (period.Filing is not { } filing)
        {
            return MissingPoint(period.ReferenceDate);
        }

        var rule = rules[filing.Id];

        return rule is NotApplicable
            ? NotApplicablePoint(period.ReferenceDate)
            : ToPoint(period.ReferenceDate, Read(filing, rule, facts[filing.Id].ToList()));
    }

    // ADR 0008 point 3: a period missing from the chosen basis is reported,
    // never filled from the other basis's filing.
    private static MetricPoint MissingPoint(DateOnly periodEnd) => ToPoint(periodEnd, null);

    // Null when the rule gives no trustworthy value from these facts.
    private Reading? Read(Filing filing, MetricRule rule, List<FinancialFact> facts)
    {
        // More than one row for an account (seen once, 2023: two periods) is as
        // untrustworthy as none.
        FinancialFact? One(string code) =>
            facts.Where(f => f.AccountCode == code).ToList() is [var only] ? only : null;

        switch (rule)
        {
            case Account account when One(account.Code) is { } fact:
                return new Reading(fact.Value, IsCalculated: false, [fact]);

            case TotalMinus totalMinus when One(totalMinus.Total) is { } total && One(totalMinus.Minority) is { } minority:
                return new Reading(total.Value - minority.Value, IsCalculated: true, [total, minority]);

            case CheckedPart checkedPart when One(checkedPart.Part) is { } part
                && One(checkedPart.Minority) is { } minority
                && One(checkedPart.Total) is { } total:
                if (Math.Abs(part.Value + minority.Value - total.Value) <= AddUpTolerance)
                {
                    return new Reading(part.Value, IsCalculated: false, [part]);
                }

                LogPartsDoNotAddUp(logger, filing.CvmCode, filing.ReferenceDate, checkedPart.Part, part.Value,
                    checkedPart.Minority, minority.Value, checkedPart.Total, total.Value);
                return null;

            default:
                // NotSupported (unknown layout) or a row missing: never zero, never a guess.
                return null;
        }
    }

    // A fact's Version is its filing's version, which is what lets a calculated
    // point built from different filings name each one.
    private static MetricPoint ToPoint(DateOnly periodEnd, Reading? reading) => reading is null
        ? new(periodEnd, MetricPointStatus.Unavailable, null, false, NoSources)
        : new(periodEnd, MetricPointStatus.Value, reading.Value, reading.IsCalculated,
            reading.Sources.Select(f => new MetricSource(f.AccountCode, f.SourceFile, f.Version, f.RetrievedAt)).ToList());

    private static MetricPoint NotApplicablePoint(DateOnly periodEnd) =>
        new(periodEnd, MetricPointStatus.NotApplicable, null, false, NoSources);

    private static IEnumerable<string> AccountsOf(MetricRule rule) => rule switch
    {
        Account a => [a.Code],
        TotalMinus t => [t.Total, t.Minority],
        CheckedPart c => [c.Part, c.Minority, c.Total],
        _ => []
    };

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Filing {CvmCode} {ReferenceDate}: {Part} ({PartValue}) + {Minority} ({MinorityValue}) does not equal {Total} ({TotalValue}). Reported as unavailable.")]
    private static partial void LogPartsDoNotAddUp(ILogger logger, int cvmCode, DateOnly referenceDate,
        string part, decimal partValue, string minority, decimal minorityValue, string total, decimal totalValue);
}
