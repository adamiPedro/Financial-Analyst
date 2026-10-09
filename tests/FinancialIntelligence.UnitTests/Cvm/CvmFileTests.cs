using FinancialIntelligence.Infrastructure.Cvm;

namespace FinancialIntelligence.UnitTests.Cvm;

// Paths as published on dados.cvm.gov.br, checked 5 Oct 2026.
public class CvmFileTests
{
    [Fact]
    public void The_company_list()
    {
        Assert.Equal("cad_cia_aberta.csv", CvmFile.Cad.FileName);
        Assert.Equal("CIA_ABERTA/CAD/DADOS/cad_cia_aberta.csv", CvmFile.Cad.UrlPath);
    }

    [Fact]
    public void An_annual_statements_file()
    {
        var file = CvmFile.Dfp(2024);

        Assert.Equal("dfp_cia_aberta_2024.zip", file.FileName);
        Assert.Equal("CIA_ABERTA/DOC/DFP/DADOS/dfp_cia_aberta_2024.zip", file.UrlPath);
    }

    [Fact]
    public void A_quarterly_statements_file()
    {
        var file = CvmFile.Itr(2025);

        Assert.Equal("itr_cia_aberta_2025.zip", file.FileName);
        Assert.Equal("CIA_ABERTA/DOC/ITR/DADOS/itr_cia_aberta_2025.zip", file.UrlPath);
    }
}
