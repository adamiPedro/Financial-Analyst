using System.Text;
using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Infrastructure.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialIntelligence.IntegrationTests.Companies;

public sealed class CompanyImportTests : IAsyncLifetime
{
    private const int CompaniesInFixture = 11;
    private const int Petrobras = 9512;
    private const string PetrobrasName = "PETRÓLEO BRASILEIRO S.A. - PETROBRAS";
    private static readonly Cnpj PetrobrasCnpj = Cnpj.Parse("33.000.167/0001-01");
    private static readonly Cnpj ValeCnpj = Cnpj.Parse("33.592.510/0001-54");

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private async Task<CompanyImportResult> ImportAsync(Stream csv)
    {
        await using var db = _database.CreateContext();
        return await new CompanyImporter(db, NullLogger<CompanyImporter>.Instance)
            .ImportAsync(csv, CancellationToken.None);
    }

    private async Task<CompanyImportResult> ImportFixtureAsync()
    {
        await using var file = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", "cad_cia_aberta.csv"));
        return await ImportAsync(file);
    }

    private async Task AddAsync(Company company)
    {
        await using var db = _database.CreateContext();
        db.Companies.Add(company);
        await db.SaveChangesAsync();
    }

    private async Task<Company> FindAsync(int cvmCode)
    {
        await using var db = _database.CreateContext();
        return await db.Companies.SingleAsync(c => c.CvmCode == cvmCode);
    }

    private async Task<int> CountAsync()
    {
        await using var db = _database.CreateContext();
        return await db.Companies.CountAsync();
    }

    [Fact]
    public async Task The_first_import_adds_every_company_in_the_file()
    {
        Assert.Equal(new CompanyImportResult(CompaniesInFixture, 0, 0), await ImportFixtureAsync());
        Assert.Equal(CompaniesInFixture, await CountAsync());
    }

    [Fact]
    public async Task Importing_the_same_file_twice_changes_nothing()
    {
        await ImportFixtureAsync();

        Assert.Equal(new CompanyImportResult(0, 0, 0), await ImportFixtureAsync());
        Assert.Equal(CompaniesInFixture, await CountAsync());
    }

    [Fact]
    public async Task A_changed_status_is_updated_and_the_ticker_is_kept()
    {
        await AddAsync(new Company(Petrobras, PetrobrasCnpj, PetrobrasName,
            ticker: "PETR4", sector: "Petróleo e Gás", registrationStatus: "CANCELADA"));

        Assert.Equal(new CompanyImportResult(CompaniesInFixture - 1, 1, 0), await ImportFixtureAsync());

        var petrobras = await FindAsync(Petrobras);
        Assert.Equal("ATIVO", petrobras.RegistrationStatus);
        Assert.Equal("PETR4", petrobras.Ticker);
    }

    [Fact]
    public async Task A_changed_name_is_updated()
    {
        await AddAsync(new Company(Petrobras, PetrobrasCnpj, "PETROBRAS",
            sector: "Petróleo e Gás", registrationStatus: "ATIVO"));

        Assert.Equal(new CompanyImportResult(CompaniesInFixture - 1, 1, 0), await ImportFixtureAsync());
        Assert.Equal(PetrobrasName, (await FindAsync(Petrobras)).Name);
    }

    [Fact]
    public async Task A_changed_cnpj_is_updated_and_counted()
    {
        await AddAsync(new Company(Petrobras, ValeCnpj, PetrobrasName,
            sector: "Petróleo e Gás", registrationStatus: "ATIVO"));

        Assert.Equal(new CompanyImportResult(CompaniesInFixture - 1, 1, 1), await ImportFixtureAsync());
        Assert.Equal(PetrobrasCnpj, (await FindAsync(Petrobras)).Cnpj);
    }

    [Fact]
    public async Task A_file_with_one_bad_row_saves_nothing()
    {
        var csv = new MemoryStream(Encoding.Latin1.GetBytes(
            "CNPJ_CIA;DENOM_SOCIAL;SIT;CD_CVM;SETOR_ATIV;TP_MERC\r\n" +
            "33.000.167/0001-01;PETRÓLEO BRASILEIRO S.A. - PETROBRAS;ATIVO;9512;Petróleo e Gás;BOLSA\r\n" +
            "33.592.510/0001-55;VALE S.A.;ATIVO;4170;Extração Mineral;BOLSA\r\n"));

        await Assert.ThrowsAsync<InvalidDataException>(() => ImportAsync(csv));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task A_row_the_database_rejects_saves_nothing_from_the_file()
    {
        // The reader accepts a 41-character status; the column holds 40, so
        // Postgres rejects Vale after Petrobras is already queued.
        var csv = new MemoryStream(Encoding.Latin1.GetBytes(
            "CNPJ_CIA;DENOM_SOCIAL;SIT;CD_CVM;SETOR_ATIV;TP_MERC\r\n" +
            "33.000.167/0001-01;PETRÓLEO BRASILEIRO S.A. - PETROBRAS;ATIVO;9512;Petróleo e Gás;BOLSA\r\n" +
            $"33.592.510/0001-54;VALE S.A.;{new string('X', 41)};4170;Extração Mineral;BOLSA\r\n"));

        await Assert.ThrowsAsync<DbUpdateException>(() => ImportAsync(csv));
        Assert.Equal(0, await CountAsync());
    }

    [Fact]
    public async Task A_company_missing_from_the_file_is_kept()
    {
        await AddAsync(new Company(99999, ValeCnpj, "EMPRESA FORA DO CADASTRO"));

        await ImportFixtureAsync();

        Assert.Equal("EMPRESA FORA DO CADASTRO", (await FindAsync(99999)).Name);
        Assert.Equal(CompaniesInFixture + 1, await CountAsync());
    }
}
