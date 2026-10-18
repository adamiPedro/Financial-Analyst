using System.Net;
using FinancialIntelligence.Application.Ingestion;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Companies;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.FinancialData;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinancialIntelligence.Infrastructure.Ingestion;

/// <summary>
/// One sync: the company list, then every DFP and ITR year, each file
/// downloaded and imported on its own. A file is imported even when CVM says
/// it hasn't changed: an import that crashed after its download, or a company
/// newly added to the tracked list, would otherwise never be loaded. The
/// importers are idempotent, so the cost is reading time only.
/// </summary>
public sealed partial class IngestionSync(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    IOptions<StatementsOptions> options,
    ILogger<IngestionSync> logger) : IIngestionSync
{
    private static readonly (FilingForm Form, int FirstPublished, Func<int, CvmFile> FileFor)[] Forms =
    [
        (FilingForm.Dfp, CvmFile.FirstDfpYear, CvmFile.Dfp),
        (FilingForm.Itr, CvmFile.FirstItrYear, CvmFile.Itr),
    ];

    public async Task<IngestionSyncResult> RunAsync(CancellationToken cancellationToken)
    {
        var settings = options.Value;
        var tracked = settings.TrackedCompanies.ToHashSet();
        var currentYear = clock.GetUtcNow().Year;
        var tally = new Tally();

        await StepAsync(CvmFile.Cad, mayBeUnpublished: false, tally, async (services, download) =>
        {
            await using var csv = File.OpenRead(download.LocalPath);
            await services.GetRequiredService<CompanyImporter>().ImportAsync(csv, cancellationToken);
            return 0;
        }, cancellationToken);

        foreach (var (form, firstPublished, fileFor) in Forms)
        {
            for (var year = Math.Max(settings.FirstYear, firstPublished); year <= currentYear; year++)
            {
                await StepAsync(fileFor(year), mayBeUnpublished: year == currentYear, tally, async (services, download) =>
                {
                    var imported = await services.GetRequiredService<StatementImporter>()
                        .ImportAsync(download.LocalPath, form, download.RetrievedAt, tracked, cancellationToken);
                    return imported.FilingsAdded;
                }, cancellationToken);
            }
        }

        var result = new IngestionSyncResult(tally.Imported, tally.NotPublished, tally.Failed, tally.FilingsAdded);
        LogFinished(logger, result.FilesImported, result.FilesNotPublished, result.FilesFailed, result.FilingsAdded);
        return result;
    }

    // A fresh scope, so a fresh DbContext, per file. After a failed SaveChanges
    // the failed entities stay tracked, and a shared context would try them
    // again on the next file's save: one bad file would sink the rest of the run.
    private async Task StepAsync(
        CvmFile file,
        bool mayBeUnpublished,
        Tally tally,
        Func<IServiceProvider, DownloadResult, Task<int>> import,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var download = await scope.ServiceProvider.GetRequiredService<CvmDownloader>()
                .DownloadAsync(file, cancellationToken);
            tally.FilingsAdded += await import(scope.ServiceProvider, download);
            tally.Imported++;
        }
        catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound && mayBeUnpublished)
        {
            // Normal from January until the year's first filing (ITR: around May).
            tally.NotPublished++;
            LogNotPublished(logger, file.FileName);
        }
    }

    private sealed class Tally
    {
        public int Imported { get; set; }
        public int NotPublished { get; set; }
        public int Failed { get; set; }
        public int FilingsAdded { get; set; }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "{FileName} is not published yet; skipped.")]
    private static partial void LogNotPublished(ILogger logger, string fileName);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Ingestion finished: {Imported} files imported, {NotPublished} not published yet, {Failed} failed, {FilingsAdded} filings added.")]
    private static partial void LogFinished(ILogger logger, int imported, int notPublished, int failed, int filingsAdded);
}
