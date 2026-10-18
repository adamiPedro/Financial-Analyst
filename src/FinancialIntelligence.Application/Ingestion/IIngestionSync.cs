namespace FinancialIntelligence.Application.Ingestion;

public interface IIngestionSync
{
    Task<IngestionSyncResult> RunAsync(CancellationToken cancellationToken);
}
