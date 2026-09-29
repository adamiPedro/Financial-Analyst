using FinancialIntelligence.Domain.Companies;

namespace FinancialIntelligence.UnitTests.Companies;

public class CompanyTests
{
    private static Company Petrobras() =>
        new(9512, Cnpj.Parse("33.000.167/0001-01"), "Petróleo Brasileiro S.A. Petrobras",
            "petr4", "Petróleo, Gás e Biocombustíveis", "ATIVO");

    [Fact]
    public void Ticker_is_uppercased()
    {
        Assert.Equal("PETR4", Petrobras().Ticker);
    }

    [Fact]
    public void A_company_may_exist_before_its_ticker_is_known()
    {
        // CVM publishes no tickers, so the registry import creates companies with
        // none and B3 data fills them in later.
        var company = new Company(9512, Cnpj.Parse("33000167000101"), "Petrobras");
        Assert.Null(company.Ticker);

        company.AssignTicker("PETR4");
        Assert.Equal("PETR4", company.Ticker);
    }

    [Fact]
    public void Whitespace_only_optional_fields_are_stored_as_absent()
    {
        var company = new Company(9512, Cnpj.Parse("33000167000101"), "Petrobras", "  ", "  ");
        Assert.Null(company.Ticker);
        Assert.Null(company.Sector);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Cvm_code_must_be_positive(int cvmCode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Company(cvmCode, Cnpj.Parse("33000167000101"), "Petrobras"));
    }

    [Fact]
    public void Name_is_required()
    {
        Assert.Throws<ArgumentException>(() =>
            new Company(9512, Cnpj.Parse("33000167000101"), "  "));
    }

    [Fact]
    public void Rename_keeps_the_same_registration()
    {
        var company = new Company(22470, Cnpj.Parse("47.960.950/0001-21"), "Magazine Luiza S.A.");
        company.Rename("Magalu S.A.");

        Assert.Equal("Magalu S.A.", company.Name);
        Assert.Equal(22470, company.CvmCode);
    }
}
