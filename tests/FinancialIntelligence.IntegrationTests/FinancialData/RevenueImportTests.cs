using System.Text;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.FinancialData;
using Microsoft.EntityFrameworkCore;

namespace FinancialIntelligence.IntegrationTests.FinancialData;

public sealed class RevenueImportTests : IAsyncLifetime
{
    private const int Petrobras = 9512;
    private const string FixtureName = "dfp_cia_aberta_DRE_con_2024.csv";
    private static readonly DateTimeOffset Downloaded = new(2026, 9, 29, 0, 0, 0, TimeSpan.Zero);

    private readonly TestDatabase _database = new();

    public Task InitializeAsync() => _database.InitializeAsync();

    public Task DisposeAsync() => _database.DisposeAsync();

    private async Task<int> ImportAsync(Stream csv)
    {
        await using var db = _database.CreateContext();
        return await new RevenueImporter(db).ImportAsync(csv, Petrobras, FixtureName, Downloaded, CancellationToken.None);
    }

    private static FileStream OpenFixture() =>
        File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", FixtureName));

    [Fact]
    public async Task Importing_the_same_file_twice_stores_revenue_once()
    {
        await _database.AddPetrobrasAsync();

        await using (var first = OpenFixture())
        {
            Assert.Equal(1, await ImportAsync(first));
        }

        await using (var second = OpenFixture())
        {
            Assert.Equal(0, await ImportAsync(second));
        }

        await using var db = _database.CreateContext();
        Assert.Equal(1, await db.FinancialFacts.CountAsync());
    }

    [Fact]
    public async Task Imported_revenue_comes_back_with_where_it_came_from()
    {
        await _database.AddPetrobrasAsync();
        await using (var file = OpenFixture())
        {
            await ImportAsync(file);
        }

        await using var db = _database.CreateContext();
        var point = Assert.Single((await new RevenueQuery(db).GetAnnualRevenueAsync(Petrobras, CancellationToken.None))!);

        Assert.Equal(490_829_000_000m, point.Value);
        Assert.Equal(new DateOnly(2024, 12, 31), point.PeriodEnd);
        Assert.Equal(AccountingBasis.Consolidated, point.Basis);
        Assert.Equal(1, point.Version);
        Assert.Equal(FixtureName, point.SourceFile);
        Assert.Equal(Downloaded, point.RetrievedAt);
    }

    [Fact]
    public async Task A_resubmitted_filing_is_kept_beside_the_original_and_wins()
    {
        await _database.AddPetrobrasAsync();
        await using (var original = OpenFixture())
        {
            await ImportAsync(original);
        }

        // Petrobras's own 2024 revenue row, as if resubmitted as version 2 with a
        // corrected figure.
        var resubmission = Latin1(
            "CNPJ_CIA;DT_REFER;VERSAO;DENOM_CIA;CD_CVM;GRUPO_DFP;MOEDA;ESCALA_MOEDA;ORDEM_EXERC;DT_INI_EXERC;DT_FIM_EXERC;CD_CONTA;DS_CONTA;VL_CONTA;ST_CONTA_FIXA\r\n" +
            "33.000.167/0001-01;2024-12-31;2;PETROLEO BRASILEIRO S.A. PETROBRAS;009512;DF Consolidado - Demonstração do Resultado;REAL;MIL;ÚLTIMO;2024-01-01;2024-12-31;3.01;Receita de Venda de Bens e/ou Serviços;491000000.0000000000;S\r\n");
        Assert.Equal(1, await ImportAsync(resubmission));

        await using var db = _database.CreateContext();
        Assert.Equal(2, await db.FinancialFacts.CountAsync());

        var point = Assert.Single((await new RevenueQuery(db).GetAnnualRevenueAsync(Petrobras, CancellationToken.None))!);
        Assert.Equal(2, point.Version);
        Assert.Equal(491_000_000_000m, point.Value);
    }

    [Fact]
    public async Task Revenue_for_an_unregistered_company_is_refused()
    {
        await using var file = OpenFixture();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ImportAsync(file));
    }

    [Fact]
    public async Task An_unknown_company_has_no_revenue_rather_than_empty_revenue()
    {
        await using var db = _database.CreateContext();

        Assert.Null(await new RevenueQuery(db).GetAnnualRevenueAsync(Petrobras, CancellationToken.None));
    }

    private static MemoryStream Latin1(string csv) => new(Encoding.Latin1.GetBytes(csv));
}
