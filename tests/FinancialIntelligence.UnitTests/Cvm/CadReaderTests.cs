using System.Text;
using FinancialIntelligence.Domain.Companies;
using FinancialIntelligence.Infrastructure.Cvm;

namespace FinancialIntelligence.UnitTests.Cvm;

// Expected values are read by hand from the fixture, a trimmed copy of CVM's
// cad_cia_aberta.csv (see Fixtures/Cvm/README.md).
public class CadReaderTests
{
    // Columns are looked up by name, so the hand-built files below only need
    // the ones the reader uses.
    private const string Header = "CNPJ_CIA;DENOM_SOCIAL;SIT;CD_CVM;SETOR_ATIV;TP_MERC\r\n";
    private const string PetrobrasRow = "33.000.167/0001-01;PETRÓLEO BRASILEIRO S.A. - PETROBRAS;ATIVO;9512;Petróleo e Gás;BOLSA\r\n";

    private static async Task<IReadOnlyList<CadCompany>> ReadFixture()
    {
        await using var file = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", "cad_cia_aberta.csv"));
        return await CadReader.ReadAsync(file, CancellationToken.None);
    }

    private static Task<IReadOnlyList<CadCompany>> ReadText(string csv) =>
        CadReader.ReadAsync(new MemoryStream(Encoding.Latin1.GetBytes(csv)), CancellationToken.None);

    [Fact]
    public async Task Each_company_comes_out_once_even_when_listed_on_two_markets()
    {
        var companies = await ReadFixture();

        Assert.Equal(11, companies.Count);
        Assert.Equal(11, companies.Select(c => c.CvmCode).Distinct().Count());
    }

    [Fact]
    public async Task Petrobras_comes_out_with_its_registry_details()
    {
        var petrobras = Assert.Single(await ReadFixture(), c => c.CvmCode == 9512);

        Assert.Equal(Cnpj.Parse("33.000.167/0001-01"), petrobras.Cnpj);
        Assert.Equal("PETRÓLEO BRASILEIRO S.A. - PETROBRAS", petrobras.Name);
        Assert.Equal("Petróleo e Gás", petrobras.Sector);
        Assert.Equal("ATIVO", petrobras.Status);
    }

    [Fact]
    public async Task One_cnpj_under_two_codes_gives_two_companies()
    {
        var companies = await ReadFixture();
        var oldCode = Assert.Single(companies, c => c.CvmCode == 2445);
        var newCode = Assert.Single(companies, c => c.CvmCode == 25577);

        Assert.Equal(oldCode.Cnpj, newCode.Cnpj);
        Assert.Equal("CANCELADA", oldCode.Status);
        Assert.Equal("ATIVO", newCode.Status);
    }

    [Fact]
    public async Task Status_is_read_as_latin1()
    {
        var rossi = Assert.Single(await ReadFixture(), c => c.CvmCode == 16306);

        Assert.Equal("SUSPENSO(A) - DECISÃO ADM", rossi.Status);
    }

    [Fact]
    public async Task A_blank_sector_comes_out_empty()
    {
        var oldSantander = Assert.Single(await ReadFixture(), c => c.CvmCode == 868);

        Assert.Null(oldSantander.Sector);
    }

    [Fact]
    public async Task Rows_that_differ_only_in_market_merge_however_many_there_are()
    {
        var companies = await ReadText(Header
            + PetrobrasRow
            + PetrobrasRow.Replace(";BOLSA", ";BALCÃO ORGANIZADO", StringComparison.Ordinal)
            + PetrobrasRow.Replace(";BOLSA", ";", StringComparison.Ordinal));

        Assert.Equal(9512, Assert.Single(companies).CvmCode);
    }

    [Fact]
    public async Task Rows_that_differ_only_in_columns_not_stored_merge()
    {
        var companies = await ReadText(
            Header.Replace("\r\n", ";TEL\r\n", StringComparison.Ordinal)
            + PetrobrasRow.Replace("\r\n", ";32241510\r\n", StringComparison.Ordinal)
            + PetrobrasRow.Replace("\r\n", ";39994000\r\n", StringComparison.Ordinal));

        Assert.Equal(9512, Assert.Single(companies).CvmCode);
    }

    [Fact]
    public async Task Rows_for_one_code_that_disagree_stop_the_read()
    {
        var csv = Header + PetrobrasRow
            + PetrobrasRow.Replace(";ATIVO;", ";CANCELADA;", StringComparison.Ordinal);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ReadText(csv));
        Assert.Contains("9512", error.Message, StringComparison.Ordinal);
        Assert.Contains("ATIVO", error.Message, StringComparison.Ordinal);
        Assert.Contains("CANCELADA", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("33.000.167/0001-02;PETROBRAS;ATIVO;9512;Petróleo e Gás;BOLSA")] // bad check digit
    [InlineData("33.000.167/0001-01;PETROBRAS;ATIVO;95X2;Petróleo e Gás;BOLSA")] // code not a number
    [InlineData("33.000.167/0001-01;  ;ATIVO;9512;Petróleo e Gás;BOLSA")]         // blank name
    [InlineData("33.000.167/0001-01;PETROBRAS;ATIVO;9512;Petróleo e Gás")]        // missing field
    [InlineData("33.000.167/0001-01;PETRO;BRAS;ATIVO;9512;Petróleo e Gás;BOLSA")] // stray ';'
    public async Task A_bad_row_stops_the_read_and_names_its_line(string row)
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => ReadText(Header + row + "\r\n"));

        Assert.Contains("Line 2", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_column_stops_the_read_and_names_the_column()
    {
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            ReadText("CNPJ_CIA;DENOM_SOCIAL;CD_CVM;SETOR_ATIV;TP_MERC\r\n"));

        Assert.Contains("SIT", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_empty_file_stops_the_read()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => ReadText(""));
    }

    [Fact]
    public async Task A_header_only_file_has_no_companies()
    {
        Assert.Empty(await ReadText(Header));
    }

    [Fact]
    public async Task Blank_lines_are_skipped()
    {
        var companies = await ReadText(Header + "\r\n" + PetrobrasRow + "\r\n");

        Assert.Single(companies);
    }
}
