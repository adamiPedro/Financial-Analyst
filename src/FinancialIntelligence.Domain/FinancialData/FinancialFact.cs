namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// One reported value for one company, account and period, as a specific
/// filing version stated it.
/// </summary>
public sealed class FinancialFact
{
    private FinancialFact()
    {
        AccountCode = null!;
        SourceFile = null!;
        Filing = null!;
    }

    /// <summary>
    /// Company, basis, version and provenance are copied from the filing rather
    /// than passed in, so a fact can never disagree with the filing it belongs to.
    /// </summary>
    public FinancialFact(Filing filing, string accountCode, DateOnly? periodStart, DateOnly periodEnd, decimal value)
    {
        ArgumentNullException.ThrowIfNull(filing);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);
        if (periodStart is { } start && periodEnd < start)
        {
            throw new ArgumentException("Period ends before it starts.", nameof(periodEnd));
        }

        Filing = filing;
        CvmCode = filing.CvmCode;
        Basis = filing.Basis;
        Version = filing.Version;
        SourceFile = filing.SourceFile;
        RetrievedAt = filing.RetrievedAt;
        AccountCode = accountCode;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Value = value;
    }

    public long Id { get; private set; }

    public long FilingId { get; private set; }

    /// <summary>
    /// Set when the fact is created. A fact read back from the database has it
    /// only if the query asked for it; queries normally go by FilingId.
    /// </summary>
    public Filing Filing { get; private set; }

    public int CvmCode { get; private set; }

    /// <summary>CD_CONTA, e.g. 3.01. Kept as published so every value traces back to its row.</summary>
    public string AccountCode { get; private set; }

    public AccountingBasis Basis { get; private set; }

    /// <summary>
    /// Both ends are stored because the end alone is ambiguous: a quarterly
    /// filing reports the three months and the year-to-date ending on the
    /// same day. Null for balance-sheet rows: a balance is a position on one
    /// date, and CVM publishes no start for it.
    /// </summary>
    public DateOnly? PeriodStart { get; private set; }

    public DateOnly PeriodEnd { get; private set; }

    /// <summary>
    /// VERSAO. A resubmitted filing is stored as a new fact beside the old one,
    /// never written over it; readers pick the highest version.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>In reais, scale already applied.</summary>
    public decimal Value { get; private set; }

    public string SourceFile { get; private set; }

    /// <summary>When the source file was downloaded, not when it was imported.</summary>
    public DateTimeOffset RetrievedAt { get; private set; }
}
