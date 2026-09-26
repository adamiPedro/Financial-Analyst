namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// CVM publishes every statement twice: consolidated and individual. They are
/// different numbers for the same period, so a fact is meaningless without
/// knowing which basis produced it. See docs/decisions/0008.
/// </summary>
public enum AccountingBasis
{
    /// <summary>
    /// Includes subsidiaries. What analysts mean by a company's revenue, and the
    /// default everywhere in this system.
    /// </summary>
    Consolidated = 1,

    /// <summary>
    /// Parent company only. Used when a company files no consolidated statements,
    /// which is common when there are no subsidiaries to consolidate.
    /// </summary>
    Individual = 2
}
