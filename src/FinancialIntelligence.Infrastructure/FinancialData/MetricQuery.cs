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

        var filings = await db.Filings
            .AsNoTracking()
            .Where(f => f.CvmCode == cvmCode && f.Form == FilingForm.Dfp)
            .ToListAsync(cancellationToken);

        // ADR 0008: consolidated if the company has any consolidated filing,
        // otherwise individual - one basis for the whole series, never mixed.
        var basis = filings.Any(f => f.Basis == AccountingBasis.Consolidated)
            ? AccountingBasis.Consolidated
            : AccountingBasis.Individual;

        var latest = filings
            .Where(f => f.Basis == basis)
            .GroupBy(f => f.ReferenceDate)
            .Select(year => year.MaxBy(f => f.Version)!)
            .OrderBy(f => f.ReferenceDate)
            .ToList();

        // Each filing can need different accounts: the layout decides, per year.
        var rules = latest.ToDictionary(f => f.Id, f => MetricMap.Find(f.Layout, f.Basis, metric));
        var filingIds = latest.Select(f => f.Id).ToList();
        var codes = rules.Values.SelectMany(AccountsOf).Distinct().ToList();

        var facts = (await db.FinancialFacts
                .AsNoTracking()
                .Where(f => filingIds.Contains(f.FilingId) && codes.Contains(f.AccountCode))
                .ToListAsync(cancellationToken))
            .ToLookup(f => f.FilingId);

        var points = latest.Select(f => PointFor(f, rules[f.Id], facts[f.Id].ToList())).ToList();

        return new MetricSeries(
            metric,
            basis,
            points.Count > 0 && points.All(p => p.Status == MetricPointStatus.NotApplicable),
            points);
    }

    private MetricPoint PointFor(Filing filing, MetricRule rule, List<FinancialFact> facts)
    {
        // More than one row for an account (seen once, 2023: two periods) is as
        // untrustworthy as none.
        FinancialFact? One(string code) =>
            facts.Where(f => f.AccountCode == code).ToList() is [var only] ? only : null;

        switch (rule)
        {
            case NotApplicable:
                return new MetricPoint(filing.ReferenceDate, MetricPointStatus.NotApplicable, null, false, null, NoSources);

            case Account account when One(account.Code) is { } fact:
                return Value(filing, fact.Value, isCalculated: false, fact);

            case TotalMinus totalMinus when One(totalMinus.Total) is { } total && One(totalMinus.Minority) is { } minority:
                return Value(filing, total.Value - minority.Value, isCalculated: true, total, minority);

            case CheckedPart checkedPart when One(checkedPart.Part) is { } part
                && One(checkedPart.Minority) is { } minority
                && One(checkedPart.Total) is { } total:
                if (Math.Abs(part.Value + minority.Value - total.Value) <= AddUpTolerance)
                {
                    return Value(filing, part.Value, isCalculated: false, part);
                }

                LogPartsDoNotAddUp(logger, filing.CvmCode, filing.ReferenceDate, checkedPart.Part, part.Value,
                    checkedPart.Minority, minority.Value, checkedPart.Total, total.Value);
                return Unavailable(filing);

            default:
                // NotSupported (unknown layout) or a row missing: never zero, never a guess.
                return Unavailable(filing);
        }
    }

    private static MetricPoint Value(Filing filing, decimal value, bool isCalculated, params FinancialFact[] sources) =>
        new(filing.ReferenceDate, MetricPointStatus.Value, value, isCalculated, filing.Version,
            sources.Select(f => new MetricSource(f.AccountCode, f.SourceFile, f.RetrievedAt)).ToList());

    private static MetricPoint Unavailable(Filing filing) =>
        new(filing.ReferenceDate, MetricPointStatus.Unavailable, null, false, null, NoSources);

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
