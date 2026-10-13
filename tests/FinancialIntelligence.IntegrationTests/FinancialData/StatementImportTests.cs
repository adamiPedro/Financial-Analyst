using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.FinancialData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialIntelligence.IntegrationTests.FinancialData;

// Counts are read from the fixture files (see Fixtures/Cvm/README.md).
public sealed class StatementImportTests : IAsyncLifetime, IDisposable
{
    private const int Petrobras = 9512;
    private const string Dfp2024 = "dfp_cia_aberta_2024.zip";
    private const string PetrobrasRow = "33.000.167/0001-01;2024-12-31;1;";
    private static readonly DateTimeOffset Downloaded = new(2026, 10, 13, 0, 0, 0, TimeSpan.Zero);
    private static readonly HashSet<int> FixtureCompanies = [9512, 19348, 906, 23159];

    // Current-period rows per 2024 filing; none repeats within a filing.
    private const int FactsIn2024 = 173 + 172 + 283 + 283 + 208 + 212 + 243 + 227;

    private static readonly int[] ResubmittedVersions = [1, 1, 2, 2];

    private readonly TestDatabase _database = new();
    private readonly FixtureZip _zips = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync() => await _database.DisposeAsync();

    public void Dispose() => _zips.Dispose();

    private async Task<StatementImportResult> ImportAsync(string zipPath, FilingForm form = FilingForm.Dfp, IReadOnlySet<int>? codes = null)
    {
        await using var db = _database.CreateContext();
        return await new StatementImporter(db, NullLogger<StatementImporter>.Instance)
            .ImportAsync(zipPath, form, Downloaded, codes ?? FixtureCompanies, CancellationToken.None);
    }

    private async Task<List<Filing>> FilingsAsync()
    {
        await using var db = _database.CreateContext();
        return await db.Filings.OrderBy(f => f.CvmCode).ThenBy(f => f.Basis).ToListAsync();
    }

    private async Task<int> FactCountAsync()
    {
        await using var db = _database.CreateContext();
        return await db.FinancialFacts.CountAsync();
    }

    [Fact]
    public async Task The_2024_annual_file_stores_every_filing_with_its_layout()
    {
        await _database.AddFixtureCompaniesAsync();

        var result = await ImportAsync(_zips.Create("dfp_2024", Dfp2024));

        Assert.Equal(new StatementImportResult(8, 0, 0, 0), result);
        Assert.Equal(FactsIn2024, await FactCountAsync());

        var layouts = (await FilingsAsync()).ToDictionary(f => (f.CvmCode, f.Basis), f => f.Layout);
        Assert.Equal(StatementLayout.Industrial, layouts[(9512, AccountingBasis.Consolidated)]);
        Assert.Equal(StatementLayout.Bank309, layouts[(19348, AccountingBasis.Consolidated)]);
        Assert.Equal(StatementLayout.Bank313, layouts[(19348, AccountingBasis.Individual)]);
        Assert.Equal(StatementLayout.Bank311, layouts[(906, AccountingBasis.Consolidated)]);
        Assert.Equal(StatementLayout.Insurer, layouts[(23159, AccountingBasis.Consolidated)]);

        var petrobras = (await FilingsAsync()).First(f => f.CvmCode == Petrobras);
        Assert.Equal(FilingForm.Dfp, petrobras.Form);
        Assert.Equal(new DateOnly(2024, 12, 31), petrobras.ReferenceDate);
        Assert.Equal(1, petrobras.Version);
        Assert.Equal(Dfp2024, petrobras.SourceFile);
        Assert.Equal(Downloaded, petrobras.RetrievedAt);
    }

    [Fact]
    public async Task Importing_the_same_file_twice_adds_nothing_balances_included()
    {
        await _database.AddFixtureCompaniesAsync();
        var zip = _zips.Create("dfp_2024", Dfp2024);
        await ImportAsync(zip);

        Assert.Equal(new StatementImportResult(0, 8, 0, 0), await ImportAsync(zip));
        Assert.Equal(FactsIn2024, await FactCountAsync());
    }

    [Fact]
    public async Task Quarterly_income_rows_are_stored_for_three_months_and_year_to_date()
    {
        await _database.AddFixtureCompaniesAsync();

        var result = await ImportAsync(_zips.Create("itr_2024", "itr_cia_aberta_2024.zip"), FilingForm.Itr, new HashSet<int> { Petrobras });

        Assert.Equal(new StatementImportResult(6, 0, 0, 0), result);
        await using var db = _database.CreateContext();
        var q2Revenue = await db.FinancialFacts
            .Where(f => f.AccountCode == "3.01" && f.Basis == AccountingBasis.Consolidated && f.PeriodEnd == new DateOnly(2024, 6, 30))
            .OrderBy(f => f.PeriodStart)
            .Select(f => new { f.PeriodStart, f.Value })
            .ToListAsync();
        Assert.Equal(2, q2Revenue.Count);
        Assert.Equal((new DateOnly(2024, 1, 1), 239_979_000_000m), (q2Revenue[0].PeriodStart!.Value, q2Revenue[0].Value));
        Assert.Equal((new DateOnly(2024, 4, 1), 122_258_000_000m), (q2Revenue[1].PeriodStart!.Value, q2Revenue[1].Value));
        Assert.All(await db.Filings.ToListAsync(), f => Assert.Equal(FilingForm.Itr, f.Form));
    }

    [Fact]
    public async Task A_resubmitted_filing_is_stored_beside_the_original()
    {
        await _database.AddFixtureCompaniesAsync();
        await ImportAsync(_zips.Create("dfp_2024", Dfp2024));

        var resubmitted = _zips.Create("dfp_2024", Dfp2024, (_, line) =>
            [line.Replace(PetrobrasRow, "33.000.167/0001-01;2024-12-31;2;", StringComparison.Ordinal)]);

        Assert.Equal(new StatementImportResult(2, 6, 0, 0), await ImportAsync(resubmitted));
        var petrobras = (await FilingsAsync()).Where(f => f.CvmCode == Petrobras).ToList();
        Assert.Equal(4, petrobras.Count);
        Assert.Equal(ResubmittedVersions, petrobras.Select(f => f.Version).Order());
    }

    [Fact]
    public async Task A_filing_in_an_unknown_layout_keeps_its_facts()
    {
        await _database.AddFixtureCompaniesAsync();
        var renamed = _zips.Create("dfp_2024", Dfp2024, (_, line) =>
            [line.Replace(";3.01;Receita de Venda de Bens e/ou Serviços;", ";3.01;Receitas das Operações;", StringComparison.Ordinal)]);

        var result = await ImportAsync(renamed);

        Assert.Equal(new StatementImportResult(8, 0, 0, 2), result);
        Assert.All((await FilingsAsync()).Where(f => f.CvmCode == Petrobras), f => Assert.Equal(StatementLayout.Unknown, f.Layout));
        Assert.Equal(FactsIn2024, await FactCountAsync());
    }

    [Fact]
    public async Task Re_importing_corrects_a_stored_layout()
    {
        await _database.AddFixtureCompaniesAsync();
        var zip = _zips.Create("dfp_2024", Dfp2024);
        await ImportAsync(zip);
        await using (var db = _database.CreateContext())
        {
            var filing = await db.Filings.SingleAsync(f => f.CvmCode == Petrobras && f.Basis == AccountingBasis.Consolidated);
            filing.ChangeLayout(StatementLayout.Bank311);
            await db.SaveChangesAsync();
        }

        Assert.Equal(new StatementImportResult(0, 8, 1, 0), await ImportAsync(zip));
        Assert.All((await FilingsAsync()).Where(f => f.CvmCode == Petrobras), f => Assert.Equal(StatementLayout.Industrial, f.Layout));
    }

    [Fact]
    public async Task Companies_not_asked_for_are_skipped()
    {
        await _database.AddFixtureCompaniesAsync();

        Assert.Equal(new StatementImportResult(2, 0, 0, 0), await ImportAsync(_zips.Create("dfp_2024", Dfp2024), codes: new HashSet<int> { Petrobras }));
        Assert.All(await FilingsAsync(), f => Assert.Equal(Petrobras, f.CvmCode));
    }

    [Fact]
    public async Task A_company_missing_from_the_register_fails_before_reading()
    {
        await _database.AddPetrobrasAsync();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ImportAsync(_zips.Create("dfp_2024", Dfp2024), codes: new HashSet<int> { Petrobras, 4170 }));

        Assert.Contains("4170", error.Message, StringComparison.Ordinal);
        Assert.Empty(await FilingsAsync());
    }

    [Fact]
    public async Task A_row_the_database_rejects_saves_nothing_from_the_file()
    {
        // The reader accepts any account code; the column holds 20 characters.
        await _database.AddFixtureCompaniesAsync();
        var tooLong = _zips.Create("dfp_2024", Dfp2024, (_, line) =>
            [line.Replace(";3.03;Resultado Bruto;", ";3.03.01.01.01.01.01.01.01;Resultado Bruto;", StringComparison.Ordinal)]);

        await Assert.ThrowsAsync<DbUpdateException>(() => ImportAsync(tooLong));
        Assert.Empty(await FilingsAsync());
        Assert.Equal(0, await FactCountAsync());
    }

    [Fact]
    public async Task A_repeated_row_is_stored_once_and_a_conflicting_one_not_at_all()
    {
        await _database.AddFixtureCompaniesAsync();
        const string income = "dfp_cia_aberta_DRE_con_2024.csv";
        var exactCopy = FixtureZip.OnRow(income, PetrobrasRow, "3.01", line => [line, line]);
        var conflict = FixtureZip.OnRow(income, PetrobrasRow, "3.03", line =>
            [line, line.Replace(";246462000.0000000000;", ";1.0000000000;", StringComparison.Ordinal)]);
        var zip = _zips.Create("dfp_2024", Dfp2024, (name, line) => exactCopy(name, line).SelectMany(l => conflict(name, l)));

        Assert.Equal(new StatementImportResult(8, 0, 0, 0), await ImportAsync(zip));

        await using var db = _database.CreateContext();
        var petrobras = db.FinancialFacts.Where(f => f.CvmCode == Petrobras && f.Basis == AccountingBasis.Consolidated);
        Assert.Equal(1, await petrobras.CountAsync(f => f.AccountCode == "3.01"));
        Assert.Equal(0, await petrobras.CountAsync(f => f.AccountCode == "3.03"));
        Assert.Equal(FactsIn2024 - 1, await db.FinancialFacts.CountAsync());
    }

    [Fact]
    public async Task A_quarterly_file_passed_as_annual_is_refused()
    {
        await _database.AddFixtureCompaniesAsync();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            ImportAsync(_zips.Create("itr_2024", "itr_cia_aberta_2024.zip"), FilingForm.Dfp));

        Assert.Equal("zipPath", error.ParamName);
    }

    [Fact]
    public async Task A_dfp_zip_under_another_name_is_refused_and_stores_nothing()
    {
        await _database.AddFixtureCompaniesAsync();

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            ImportAsync(_zips.Create("dfp_2024", "statements.zip"), FilingForm.Dfp));

        Assert.Equal("zipPath", error.ParamName);
        Assert.Empty(await FilingsAsync());
    }

    [Fact]
    public async Task A_bad_row_names_the_file_inside_the_zip_and_the_line()
    {
        await _database.AddFixtureCompaniesAsync();
        var badScale = _zips.Create("dfp_2024", Dfp2024, FixtureZip.OnRow("dfp_cia_aberta_BPP_ind_2024.csv", PetrobrasRow, "2.03",
            line => [line.Replace(";MIL;", ";BILHAO;", StringComparison.Ordinal)]));

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ImportAsync(badScale));

        Assert.Contains("dfp_cia_aberta_BPP_ind_2024.csv", error.Message, StringComparison.Ordinal);
        // Line 317 of the fixture: Petrobras individual 2.03, the ÚLTIMO row (header is line 1).
        Assert.Contains("Line 317", error.Message, StringComparison.Ordinal);
        Assert.Empty(await FilingsAsync());
    }

    [Fact]
    public async Task A_missing_statement_fails_and_names_it()
    {
        await _database.AddFixtureCompaniesAsync();
        var path = _zips.Create("dfp_2024", Dfp2024);
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update))
        {
            zip.GetEntry("dfp_cia_aberta_DFC_MD_ind_2024.csv")!.Delete();
        }

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ImportAsync(path));

        Assert.Contains("dfp_cia_aberta_DFC_MD_ind_2024.csv", error.Message, StringComparison.Ordinal);
    }
}
