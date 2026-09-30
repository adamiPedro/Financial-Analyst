using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Application.FinancialData;

public sealed record RevenuePoint(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal Value,
    AccountingBasis Basis,
    int Version,
    string SourceFile,
    DateTimeOffset RetrievedAt);

public interface IRevenueQuery
{
    /// <summary>
    /// Annual revenue (CD_CONTA 3.01), oldest first, latest filing version of
    /// each year, all on one accounting basis. Null when the company is unknown;
    /// empty when it is known but no revenue has been imported.
    /// </summary>
    Task<IReadOnlyList<RevenuePoint>?> GetAnnualRevenueAsync(int cvmCode, CancellationToken cancellationToken);
}
