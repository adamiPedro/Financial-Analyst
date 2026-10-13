namespace FinancialIntelligence.Domain.FinancialData;

/// <summary>
/// One statement set a company filed with CVM: one form, reference date,
/// version and basis. Its facts belong to it.
/// </summary>
public sealed class Filing
{
    private Filing()
    {
        SourceFile = null!;
    }

    public Filing(
        int cvmCode,
        FilingForm form,
        DateOnly referenceDate,
        int version,
        AccountingBasis basis,
        StatementLayout layout,
        string sourceFile,
        DateTimeOffset retrievedAt)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cvmCode);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceFile);

        CvmCode = cvmCode;
        Form = form;
        ReferenceDate = referenceDate;
        Version = version;
        Basis = basis;
        Layout = layout;
        SourceFile = sourceFile;
        RetrievedAt = retrievedAt;
    }

    public long Id { get; private set; }

    public int CvmCode { get; private set; }

    public FilingForm Form { get; private set; }

    /// <summary>DT_REFER: the date the filing reports on. For a DFP, the fiscal year end.</summary>
    public DateOnly ReferenceDate { get; private set; }

    /// <summary>VERSAO. A resubmission is a new filing beside the original, never an overwrite.</summary>
    public int Version { get; private set; }

    public AccountingBasis Basis { get; private set; }

    /// <summary>
    /// Detected from this filing's own rows, not looked up per company, because
    /// the same company can file in different layouts in different years.
    /// </summary>
    public StatementLayout Layout { get; private set; }

    public string SourceFile { get; private set; }

    /// <summary>When the source file was downloaded, not when it was imported.</summary>
    public DateTimeOffset RetrievedAt { get; private set; }

    /// <summary>
    /// Detection rules improve over time; re-importing a stored filing applies
    /// the current rules to it without touching its facts.
    /// </summary>
    public void ChangeLayout(StatementLayout layout) => Layout = layout;
}
