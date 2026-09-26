using FinancialIntelligence.Worker;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<IngestionScheduler>();

var host = builder.Build();
host.Run();
