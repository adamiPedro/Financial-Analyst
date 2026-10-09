namespace FinancialIntelligence.Domain.Companies;

/// <summary>
/// A company registered with the CVM whose filings this system tracks.
/// </summary>
public sealed class Company
{
    /// <summary>
    /// EF Core materializes entities through the parameterless constructor and
    /// then sets every mapped property by reflection. The null-forgiving
    /// assignments satisfy nullable analysis for that window; null is never
    /// observable to application code.
    /// </summary>
    private Company()
    {
        Cnpj = null!;
        Name = null!;
    }

    public Company(
        int cvmCode,
        Cnpj cnpj,
        string name,
        string? ticker = null,
        string? sector = null,
        string? registrationStatus = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cvmCode);
        ArgumentNullException.ThrowIfNull(cnpj);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        CvmCode = cvmCode;
        Cnpj = cnpj;
        Name = name.Trim();
        Ticker = NormalizeTicker(ticker);
        Sector = Blank(sector);
        RegistrationStatus = Blank(registrationStatus);
    }

    /// <summary>
    /// CD_CVM: the registration code the CVM assigns to each open company, and
    /// the primary key. A natural key rather than a surrogate, because this
    /// system covers one jurisdiction and the code is on every row of every
    /// filing CVM publishes - so ingestion never needs a lookup to know which
    /// company a fact belongs to. See docs/decisions/0007.
    ///
    /// Assigned by CVM, never generated here. The EF configuration has to say so
    /// explicitly; left to convention, EF treats an int key as an identity column
    /// and silently discards the real code.
    /// </summary>
    public int CvmCode { get; private set; }

    /// <summary>
    /// The legal entity's tax registration. Carried as data, not identity: a CNPJ
    /// can change under corporate restructuring, and subsidiaries have their own.
    /// </summary>
    public Cnpj Cnpj { get; private set; }

    /// <summary>DENOM_SOCIAL in CAD, the registered corporate name.</summary>
    public string Name { get; private set; }

    /// <summary>
    /// B3 trading ticker, e.g. PETR4. Nullable because CVM does not publish
    /// tickers at all - it identifies companies by CNPJ and CD_CVM. Tickers come
    /// from B3 or from the curated seed list, so a company can legitimately exist
    /// here before its ticker is known.
    /// </summary>
    public string? Ticker { get; private set; }

    /// <summary>
    /// Sector of activity from the CVM company registry (CAD). Nullable because
    /// CAD leaves it blank for some registrants.
    /// </summary>
    public string? Sector { get; private set; }

    /// <summary>
    /// Registry status - active, cancelled, suspended. Matters because CVM keeps
    /// publishing historical filings for companies that have since delisted, and
    /// an analysis that silently includes them is wrong in a way nobody notices.
    /// </summary>
    public string? RegistrationStatus { get; private set; }

    public void Rename(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
    }

    /// <summary>
    /// CVM gives a re-registered company a new code, so a CNPJ changing under an
    /// existing code is unexpected. CAD is still the registry's source of truth;
    /// the importer applies the change and logs it rather than refusing it.
    /// </summary>
    public void ChangeCnpj(Cnpj cnpj)
    {
        ArgumentNullException.ThrowIfNull(cnpj);
        Cnpj = cnpj;
    }

    public void UpdateRegistry(string? sector, string? registrationStatus)
    {
        Sector = Blank(sector);
        RegistrationStatus = Blank(registrationStatus);
    }

    public void AssignTicker(string ticker)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticker);
        Ticker = NormalizeTicker(ticker);
    }

    /// <summary>
    /// B3 tickers are four letters plus a share-class digit (PETR4, ITUB4, VALE3).
    /// Uppercasing is the whole normalization - unlike US tickers there is no
    /// competing punctuation convention between sources to reconcile.
    /// </summary>
    private static string? NormalizeTicker(string? ticker) =>
        string.IsNullOrWhiteSpace(ticker) ? null : ticker.Trim().ToUpperInvariant();

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
