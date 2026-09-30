using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialIntelligence.Infrastructure.FinancialData;

internal sealed class RevenueQuery(FinancialIntelligenceDbContext db) : IRevenueQuery
{
    public async Task<IReadOnlyList<RevenuePoint>?> GetAnnualRevenueAsync(
        int cvmCode, CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.CvmCode == cvmCode, cancellationToken))
        {
            return null;
        }

        // A handful of rows per company, so the version and basis rules run in
        // memory. At scale this becomes a DISTINCT ON query.
        var facts = await db.FinancialFacts
            .AsNoTracking()
            .Where(f => f.CvmCode == cvmCode && f.AccountCode == RevenueImporter.RevenueAccount)
            .ToListAsync(cancellationToken);

        // ADR 0008: consolidated if the company has any consolidated revenue at
        // all, otherwise individual - one basis for the whole series, never mixed.
        var basis = facts.Any(f => f.Basis == AccountingBasis.Consolidated)
            ? AccountingBasis.Consolidated
            : AccountingBasis.Individual;

        return facts
            .Where(f => f.Basis == basis)
            .GroupBy(f => (f.PeriodStart, f.PeriodEnd))
            .Select(period => period.MaxBy(f => f.Version)!)
            .OrderBy(f => f.PeriodEnd)
            .Select(f => new RevenuePoint(
                f.PeriodStart, f.PeriodEnd, f.Value, f.Basis, f.Version, f.SourceFile, f.RetrievedAt))
            .ToList();
    }
}
