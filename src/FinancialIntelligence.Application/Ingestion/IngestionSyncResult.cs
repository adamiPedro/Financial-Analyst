namespace FinancialIntelligence.Application.Ingestion;

public sealed record IngestionSyncResult(int FilesImported, int FilesNotPublished, int FilesFailed, int FilingsAdded);
