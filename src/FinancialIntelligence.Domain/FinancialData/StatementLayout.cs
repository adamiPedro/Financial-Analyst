namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// CVM's account codes do not mean the same thing for every filer: banks and
/// insurers number their lines differently, and a bank can switch layouts
/// between years (Bradesco did in 2020). Named after where net income sits,
/// because that is what tells the bank layouts apart. See docs/decisions/0009.
/// </summary>
public enum StatementLayout
{
    /// <summary>Matched no known layout. Its facts are kept; no metric is read from them.</summary>
    Unknown = 0,
    Industrial = 1,
    Bank309 = 2,
    Bank311 = 3,
    Bank313 = 4,
    Insurer = 5
}
