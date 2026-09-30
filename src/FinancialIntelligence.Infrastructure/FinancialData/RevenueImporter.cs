using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinancialIntelligence.Infrastructure.FinancialData;

/// <summary>
/// M0 only: imports one company's revenue from one DFP income statement file.
/// Hardcoded to account 3.01 on purpose; the general pipeline is M1.
/// </summary>
public sealed class RevenueImporter(FinancialIntelligenceDbContext db)
{
    public const string RevenueAccount = "3.01";

    /// <returns>How many new facts were stored. Zero on a repeat import.</returns>
    public async Task<int> ImportAsync(
        Stream csv,
        int cvmCode,
        string sourceFile,
        DateTimeOffset retrievedAt,
        CancellationToken cancellationToken)
    {
        if (!await db.Companies.AnyAsync(c => c.CvmCode == cvmCode, cancellationToken))
        {
            throw new InvalidOperationException(
                $"Company {cvmCode} is not registered. Companies come from CAD, not from statement files.");
        }

        var values = await DfpStatementReader.ReadAccountAsync(csv, cvmCode, RevenueAccount, cancellationToken);

        var stored = await db.FinancialFacts
            .Where(f => f.CvmCode == cvmCode && f.AccountCode == RevenueAccount)
            .Select(f => new { f.Basis, f.PeriodStart, f.PeriodEnd, f.Version })
            .ToListAsync(cancellationToken);

        // Skipping what is already stored keeps a repeat import quiet. The unique
        // index is still the real guarantee if two imports ever race.
        var added = 0;
        foreach (var value in values)
        {
            if (stored.Any(s => s.Basis == value.Basis && s.PeriodStart == value.PeriodStart
                && s.PeriodEnd == value.PeriodEnd && s.Version == value.Version))
            {
                continue;
            }

            db.FinancialFacts.Add(new FinancialFact(
                cvmCode, RevenueAccount, value.Basis, value.PeriodStart, value.PeriodEnd,
                value.Version, value.Value, sourceFile, retrievedAt));
            added++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return added;
    }
}
