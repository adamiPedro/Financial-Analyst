using FinancialIntelligence.Api.Companies;
using FinancialIntelligence.Api.Health;
using FinancialIntelligence.Infrastructure;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Composition root.
//
// This is the ONLY place in the Api project allowed to know about Infrastructure.
// It asks for infrastructure by intent; it does not know infrastructure is EF
// Core. The only EF package here is the design-time one `dotnet ef` needs in
// the startup project, and no code in Api touches it.
// ---------------------------------------------------------------------------

var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres is not configured. See README 'Configuration'.");

builder.Services.AddInfrastructure(connectionString);

// A raw data source, separate from the DbContext, purely for the health check.
// Deliberate: a health check that goes through EF would report unhealthy for
// model or migration problems as well as connectivity ones, and those need
// different responses.
builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));

builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres");

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// The static frontend lives in wwwroot. One page, no build step (ADR 0004).
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/health");
app.MapRevenueEndpoints();

app.Run();

// Exposed so FinancialIntelligence.ApiTests can use WebApplicationFactory<Program>.
public partial class Program;
