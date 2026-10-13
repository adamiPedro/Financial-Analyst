using System.Text;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Cvm;

namespace FinancialIntelligence.UnitTests.Cvm;

// Expected values are read by hand from the fixtures (see Fixtures/Cvm/README.md).
public class StatementReaderTests
{
    private const int Petrobras = 9512;
    private const int Vivara = 24805;

    private const string IncomeHeader =
        "CNPJ_CIA;DT_REFER;VERSAO;DENOM_CIA;CD_CVM;GRUPO_DFP;MOEDA;ESCALA_MOEDA;ORDEM_EXERC;DT_INI_EXERC;DT_FIM_EXERC;CD_CONTA;DS_CONTA;VL_CONTA;ST_CONTA_FIXA";

    // Petrobras's own 2024 revenue row from dfp_cia_aberta_DRE_con_2024.csv.
    private const string PetrobrasRevenue =
        "33.000.167/0001-01;2024-12-31;1;PETROLEO BRASILEIRO S.A. PETROBRAS;009512;DF Consolidado - Demonstração do Resultado;REAL;MIL;ÚLTIMO;2024-01-01;2024-12-31;3.01;Receita de Venda de Bens e/ou Serviços;490829000.0000000000;S";

    private static async Task<IReadOnlyList<StatementRow>> ReadFixture(string path, params int[] cvmCodes)
    {
        await using var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", path));
        return await StatementReader.ReadAsync(file, cvmCodes.ToHashSet(), CancellationToken.None);
    }

    private static Task<IReadOnlyList<StatementRow>> ReadText(string csv) =>
        StatementReader.ReadAsync(new MemoryStream(Encoding.Latin1.GetBytes(csv)), new HashSet<int> { Petrobras }, CancellationToken.None);

    [Fact]
    public async Task Petrobras_2024_revenue_is_read_once_with_everything_about_it()
    {
        // The file also has 511994000 for the 2023 comparative (PENÚLTIMO), which must not come back.
        var rows = await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", Petrobras);
        var revenue = Assert.Single(rows, r => r.AccountCode == "3.01");

        Assert.Equal(new StatementRow(Petrobras, new DateOnly(2024, 12, 31), 1, AccountingBasis.Consolidated,
            "3.01", "Receita de Venda de Bens e/ou Serviços", new DateOnly(2024, 1, 1), new DateOnly(2024, 12, 31),
            490_829_000_000m), revenue);
    }

    [Fact]
    public async Task Only_current_period_rows_come_back()
    {
        var rows = await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", Petrobras);

        Assert.NotEmpty(rows);
        Assert.All(rows, r => Assert.Equal(2024, r.PeriodEnd.Year));
    }

    [Fact]
    public async Task Only_the_listed_companies_come_back()
    {
        Assert.All(await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", Petrobras), r => Assert.Equal(Petrobras, r.CvmCode));
        Assert.Empty(await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", 4170));
    }

    [Fact]
    public async Task Revenue_published_in_reais_is_not_scaled()
    {
        var revenue = Assert.Single(await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", Vivara), r => r.AccountCode == "3.01");

        Assert.Equal(2_577_113_417m, revenue.Value);
    }

    [Fact]
    public async Task Earnings_per_share_is_not_scaled_even_though_the_row_says_thousands()
    {
        // Petrobras 2024 basic EPS, ON shares: R$ 2.84, labelled MIL in the file.
        var eps = Assert.Single(await ReadFixture("dfp_cia_aberta_DRE_con_2024.csv", Petrobras), r => r.AccountCode == "3.99.01.01");

        Assert.Equal(2.84m, eps.Value);
    }

    [Fact]
    public async Task A_balance_has_no_start_date()
    {
        // Petrobras total assets, 31 Dec 2024: 1124797000 thousand.
        var assets = Assert.Single(await ReadFixture("dfp_2024/dfp_cia_aberta_BPA_con_2024.csv", Petrobras), r => r.AccountCode == "1");

        Assert.Null(assets.PeriodStart);
        Assert.Equal(new DateOnly(2024, 12, 31), assets.PeriodEnd);
        Assert.Equal(1_124_797_000_000m, assets.Value);
    }

    [Fact]
    public async Task A_file_with_only_a_header_has_no_rows()
    {
        Assert.Empty(await ReadFixture("dfp_2024/dfp_cia_aberta_DFC_MD_con_2024.csv", Petrobras));
    }

    [Fact]
    public async Task An_unknown_scale_fails_and_names_the_line()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadText($"{IncomeHeader}\r\n{PetrobrasRevenue.Replace(";MIL;", ";BILHAO;", StringComparison.Ordinal)}\r\n"));

        Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
        Assert.Contains("BILHAO", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_basis_fails_and_names_the_line()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadText($"{IncomeHeader}\r\n{PetrobrasRevenue.Replace(";DF Consolidado", ";DF Combinado", StringComparison.Ordinal)}\r\n"));

        Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_row_with_a_missing_field_fails_and_names_the_line()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadText($"{IncomeHeader}\r\n{PetrobrasRevenue}\r\n{PetrobrasRevenue[..^2]}\r\n"));

        Assert.Contains("Line 3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_file_fails()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadText(""));
    }
}
