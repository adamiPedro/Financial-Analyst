using System.Net;
using System.Net.Http.Json;
using FinancialIntelligence.Api.Companies;
using FinancialIntelligence.Infrastructure.FinancialData;
using FinancialIntelligence.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FinancialIntelligence.ApiTests;

public sealed class RevenueEndpointTests : IAsyncLifetime
{
    private readonly TestDatabase _database = new();
    private WebApplicationFactory<Program> _factory = null!;

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
            host.UseSetting("ConnectionStrings:Postgres", _database.ConnectionString));
    }

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        await _database.DisposeAsync();
    }

    [Fact]
    public async Task Petrobras_revenue_goes_in_from_the_cvm_file_and_comes_out_of_the_endpoint()
    {
        await _database.AddPetrobrasAsync();
        await using (var db = _database.CreateContext())
        await using (var file = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", "dfp_cia_aberta_DRE_con_2024.csv")))
        {
            await new RevenueImporter(db).ImportAsync(
                file, 9512, "dfp_cia_aberta_DRE_con_2024.csv", DateTimeOffset.UtcNow, CancellationToken.None);
        }

        var revenue = await _factory.CreateClient()
            .GetFromJsonAsync<RevenueResponse[]>("/api/companies/9512/revenue");

        var year = Assert.Single(revenue!);
        Assert.Equal(2024, year.Year);
        Assert.Equal(490_829_000_000m, year.ValueBrl);
        Assert.Equal("Consolidated", year.Basis);
        Assert.Equal("dfp_cia_aberta_DRE_con_2024.csv", year.SourceFile);
    }

    [Fact]
    public async Task An_unknown_company_is_not_found()
    {
        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/revenue");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
