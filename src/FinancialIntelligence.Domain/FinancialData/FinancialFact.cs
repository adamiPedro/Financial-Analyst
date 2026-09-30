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
    }

    public FinancialFact(
        int cvmCode,
        string accountCode,
        AccountingBasis basis,
        DateOnly periodStart,
        DateOnly periodEnd,
        int version,
        decimal value,
        string sourceFile,
        DateTimeOffset retrievedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cvmCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        if (periodEnd < periodStart)
        {
            throw new ArgumentException("Period ends before it starts.", nameof(periodEnd));
        }

        CvmCode = cvmCode;
        AccountCode = accountCode;
        Basis = basis;
        PeriodStart = periodStart;
        PeriodEnd = periodEnd;
        Version = version;
        Value = value;
        SourceFile = sourceFile;
        RetrievedAt = retrievedAt;
    }

    public long Id { get; private set; }

    public int CvmCode { get; private set; }

    /// <summary>CD_CONTA, e.g. 3.01. Kept as published so every value traces back to its row.</summary>
    public string AccountCode { get; private set; }

    public AccountingBasis Basis { get; private set; }

    /// <summary>
    /// Both ends are stored because the end alone is ambiguous: a quarterly
    /// filing reports the three months and the year-to-date ending on the
    /// same day.
    /// </summary>
    public DateOnly PeriodStart { get; private set; }

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
