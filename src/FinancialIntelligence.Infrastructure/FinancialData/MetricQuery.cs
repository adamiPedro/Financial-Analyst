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

    public async Task<MetricSeries?> GetAnnualAsync(int cvmCode, Metric metric, CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.CvmCode == cvmCode, cancellationToken))
        {
            return null;
        }

        var (basis, latest) = await LatestFilingsAsync(cvmCode, cancellationToken);
        var annual = latest.Where(f => f.Form == FilingForm.Dfp).ToList();
        var rules = annual.ToDictionary(f => f.Id, f => MetricMap.Find(f.Layout, f.Basis, metric));
        var facts = await FactsAsync(rules, cancellationToken);

        return Series(metric, basis, annual.Select(f => PointFor(f, rules[f.Id], facts[f.Id].ToList())).ToList());
    }

    private async Task<(AccountingBasis Basis, List<Filing> Latest)> LatestFilingsAsync(int cvmCode, CancellationToken cancellationToken)
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

        var latest = filings
            .Where(f => f.Basis == basis)
            .GroupBy(f => (f.Form, f.ReferenceDate))
            .Select(period => period.MaxBy(f => f.Version)!)
            .OrderBy(f => f.ReferenceDate)
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

    private static MetricSeries Series(Metric metric, AccountingBasis basis, List<MetricPoint> points) =>
        new(metric, basis, points.Count > 0 && points.All(p => p.Status == MetricPointStatus.NotApplicable), points);

    private MetricPoint PointFor(Filing filing, MetricRule rule, List<FinancialFact> facts) =>
        rule is NotApplicable ? NotApplicablePoint(filing) : ToPoint(filing, Read(filing, rule, facts));

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
    private static MetricPoint ToPoint(Filing filing, Reading? reading) => reading is null
        ? new(filing.ReferenceDate, MetricPointStatus.Unavailable, null, false, NoSources)
        : new(filing.ReferenceDate, MetricPointStatus.Value, reading.Value, reading.IsCalculated,
            reading.Sources.Select(f => new MetricSource(f.AccountCode, f.SourceFile, f.Version, f.RetrievedAt)).ToList());

    private static MetricPoint NotApplicablePoint(Filing filing) =>
        new(filing.ReferenceDate, MetricPointStatus.NotApplicable, null, false, NoSources);

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
