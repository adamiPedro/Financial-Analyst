using System.Net;
using System.Net.Http.Json;
using FinancialIntelligence.Api.Companies;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.FinancialData;
using FinancialIntelligence.IntegrationTests;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Logging.Abstractions;

namespace FinancialIntelligence.ApiTests;

public sealed class MetricEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixtureZip _zips = new();
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

    // xunit's IAsyncLifetime.DisposeAsync is not IAsyncDisposable, so CA1001
    // only accepts the owned disposable being released through IDisposable.
    public void Dispose() => _zips.Dispose();

    private async Task ImportPetrobras2024Async()
    {
        await _database.AddPetrobrasAsync();
        await using var db = _database.CreateContext();
        await new StatementImporter(db, NullLogger<StatementImporter>.Instance).ImportAsync(
            _zips.Create("dfp_2024", "dfp_cia_aberta_2024.zip"), FilingForm.Dfp, DateTimeOffset.UtcNow,
            new HashSet<int> { 9512 }, CancellationToken.None);
    }

    private async Task ImportPetrobrasQuarterlyAsync()
    {
        await ImportPetrobras2024Async();
        await using var db = _database.CreateContext();
        await new StatementImporter(db, NullLogger<StatementImporter>.Instance).ImportAsync(
            _zips.Create("itr_2024", "itr_cia_aberta_2024.zip"), FilingForm.Itr, DateTimeOffset.UtcNow,
            new HashSet<int> { 9512 }, CancellationToken.None);
    }

    [Fact]
    public async Task Petrobras_quarterly_revenue_comes_out_with_a_calculated_q4()
    {
        await ImportPetrobrasQuarterlyAsync();

        var series = await _factory.CreateClient()
            .GetFromJsonAsync<MetricSeriesResponse>("/api/companies/9512/metrics/revenue/quarterly");

        Assert.Equal("Consolidated", series!.Basis);
        Assert.Equal(4, series.Points.Count);
        var q4 = series.Points[3];
        Assert.Equal(new DateOnly(2024, 12, 31), q4.PeriodEnd);
        Assert.Equal(121_268_000_000m, q4.Value);
        Assert.True(q4.IsCalculated);
        Assert.Equal(["dfp_cia_aberta_2024.zip", "itr_cia_aberta_2024.zip"], q4.Sources.Select(s => s.SourceFile));
        Assert.All(q4.Sources, s => Assert.Equal(1, s.FilingVersion));
    }

    [Fact]
    public async Task An_unknown_metric_is_a_bad_request_quarterly_too()
    {
        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/metrics/ebitda/quarterly");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_company_is_not_found_quarterly_too()
    {
        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/metrics/revenue/quarterly");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Petrobras_revenue_goes_in_from_the_cvm_file_and_comes_out_of_the_endpoint()
    {
        await ImportPetrobras2024Async();

        var series = await _factory.CreateClient()
            .GetFromJsonAsync<MetricSeriesResponse>("/api/companies/9512/metrics/revenue/annual");

        Assert.Equal("revenue", series!.Metric);
        Assert.Equal("Consolidated", series.Basis);
        Assert.False(series.NotApplicable);
        var point = Assert.Single(series.Points);
        Assert.Equal(new DateOnly(2024, 12, 31), point.PeriodEnd);
        Assert.Equal("Value", point.Status);
        Assert.Equal(490_829_000_000m, point.Value);
        var source = Assert.Single(point.Sources);
        Assert.Equal("3.01", source.AccountCode);
        Assert.Equal("dfp_cia_aberta_2024.zip", source.SourceFile);
        Assert.Equal(1, source.FilingVersion);
    }

    [Fact]
    public async Task Metric_names_are_not_case_sensitive()
    {
        await ImportPetrobras2024Async();

        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/metrics/Net-Income/annual");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_metric_is_a_bad_request()
    {
        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/metrics/ebitda/annual");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_company_is_not_found()
    {
        var response = await _factory.CreateClient().GetAsync("/api/companies/9512/metrics/revenue/annual");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
