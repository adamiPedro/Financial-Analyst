using FinancialIntelligence.Infrastructure;
using FinancialIntelligence.Worker;

// dotnet run keeps the shell's directory as the current directory (checked),
// and the host reads appsettings.json from there by default: run from the repo
// root, it would find none. Config comes from the build output instead, which
// leaves the current directory free to anchor Cvm:DataDirectory ("data/raw")
// at the repo root.
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
