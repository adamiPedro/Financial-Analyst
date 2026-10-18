using FinancialIntelligence.Infrastructure;
using FinancialIntelligence.Worker;

// dotnet run starts this project in the repo root (RunWorkingDirectory in the
// csproj), so the relative Cvm:DataDirectory ("data/raw") lands there. The host
// looks for appsettings.json in the current directory by default, and there is
// none at the repo root; reading config from the build output finds it wherever
// the process starts.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Composition root: the only place in the Worker that knows Infrastructure.
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Postgres is not configured. See README 'Configuration'.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddIngestion();
builder.Services.AddIngestionScheduler();

builder.Build().Run();
