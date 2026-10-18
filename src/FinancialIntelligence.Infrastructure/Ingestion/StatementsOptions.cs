namespace FinancialIntelligence.Infrastructure.Ingestion;

/// <summary>
/// Bound from "Statements". No defaults on purpose: a Worker that hasn't been
/// told what to track should refuse to start, not quietly sync nothing.
/// </summary>
public sealed class StatementsOptions
{
    public const string Section = "Statements";

    public IReadOnlyList<int> TrackedCompanies { get; set; } = [];

    public int FirstYear { get; set; }
}
