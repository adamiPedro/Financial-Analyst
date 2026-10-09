using FinancialIntelligence.Domain.Ingestion;

namespace FinancialIntelligence.UnitTests.Ingestion;

public class SourceFileDownloadTests
{
    private static readonly string Sha = new('a', 64);
    private static readonly DateTimeOffset At = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_download_keeps_what_it_was_given()
    {
        var download = new SourceFileDownload(
            "cad_cia_aberta.csv", "https://dados.cvm.gov.br/dados/CIA_ABERTA/CAD/DADOS/cad_cia_aberta.csv",
            "\"6ac323d4-16cb49\"", Sha, 1_493_833, At);

        Assert.Equal("cad_cia_aberta.csv", download.FileName);
        Assert.Equal("\"6ac323d4-16cb49\"", download.ETag);
        Assert.Equal(Sha, download.Sha256);
        Assert.Equal(1_493_833, download.SizeBytes);
        Assert.Equal(At, download.DownloadedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData(null)]
    public void A_missing_etag_is_stored_as_absent(string? eTag)
    {
        var download = new SourceFileDownload("cad_cia_aberta.csv", "https://x/", eTag, Sha, 1, At);

        Assert.Null(download.ETag);
    }

    [Theory]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")] // upper case
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]  // 63 chars
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")] // not hex
    public void Sha256_must_be_64_lower_case_hex_characters(string sha256)
    {
        Assert.Throws<ArgumentException>(() =>
            new SourceFileDownload("cad_cia_aberta.csv", "https://x/", null, sha256, 1, At));
    }
}
