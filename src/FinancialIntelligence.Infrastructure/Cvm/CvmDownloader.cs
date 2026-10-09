using System.Net;
using System.Security.Cryptography;
using FinancialIntelligence.Domain.Ingestion;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace FinancialIntelligence.Infrastructure.Cvm;

public enum DownloadOutcome
{
    /// <summary>CVM answered 304; nothing was transferred.</summary>
    NotModified,

    /// <summary>CVM sent the file, but the bytes match the last copy (it rebuilt it unchanged).</summary>
    Unchanged,

    /// <summary>New contents, or the first download. The only outcome that needs an import.</summary>
    Updated,
}

public sealed record DownloadResult(DownloadOutcome Outcome, string LocalPath, string Sha256, DateTimeOffset RetrievedAt);

/// <summary>
/// Fetches one CVM file into the data folder. Retries and timeouts for getting a
/// response come from the resilience handler registered with this client.
/// </summary>
public sealed class CvmDownloader(
    HttpClient http,
    FinancialIntelligenceDbContext db,
    IOptions<CvmOptions> options)
{
    public async Task<DownloadResult> DownloadAsync(CvmFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        var settings = options.Value;
        Directory.CreateDirectory(settings.DataDirectory);
        var localPath = Path.Combine(settings.DataDirectory, file.FileName);
        var url = new Uri(settings.BaseUrl, file.UrlPath);

        var latest = await db.SourceFileDownloads
            .Where(d => d.FileName == file.FileName)
            .OrderByDescending(d => d.DownloadedAt)
            .ThenByDescending(d => d.Id)
            .FirstOrDefaultAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Get, url);

        // Only ask "changed since?" while we still hold the copy a 304 would
        // tell us to keep. Sent raw: it goes back exactly as CVM sent it.
        var askedIfChanged = false;
        if (latest?.ETag is { } eTag && File.Exists(localPath))
        {
            request.Headers.TryAddWithoutValidation("If-None-Match", eTag);
            askedIfChanged = true;
        }

        // Headers-read, so a 30 MB body streams to disk instead of being
        // buffered in memory first.
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotModified && askedIfChanged && latest is not null)
        {
            return new DownloadResult(DownloadOutcome.NotModified, localPath, latest.Sha256, latest.DownloadedAt);
        }

        if (response.StatusCode != HttpStatusCode.OK)
        {
            throw new HttpRequestException(
                $"CVM answered {(int)response.StatusCode} ({response.ReasonPhrase}) for {url}.",
                inner: null,
                response.StatusCode);
        }

        var tempPath = localPath + ".download";
        var (sha256, size) = await SaveBodyAsync(response, tempPath, file, settings.BodyReadTimeout, cancellationToken);

        // The file on disk counts too: a matching fingerprint for a copy that
        // was deleted would otherwise throw away the only copy.
        var outcome = latest is not null && latest.Sha256 == sha256 && File.Exists(localPath)
            ? DownloadOutcome.Unchanged
            : DownloadOutcome.Updated;

        if (outcome == DownloadOutcome.Unchanged)
        {
            File.Delete(tempPath);
        }
        else
        {
            // Same folder, so the swap is a single rename: a reader sees the old
            // file or the new one, never half of one.
            File.Move(tempPath, localPath, overwrite: true);
        }

        // Recorded only after the file is in place. A crash in between leaves a
        // new file under an old record; the next run sees a different SHA-256
        // and reports Updated - a harmless re-import. The reverse order could
        // leave a new record over an old file, and 304s would hide it for good.
        var retrievedAt = DateTimeOffset.UtcNow;
        db.SourceFileDownloads.Add(new SourceFileDownload(
            file.FileName, url.ToString(), response.Headers.ETag?.ToString(), sha256, size, retrievedAt));
        await db.SaveChangesAsync(cancellationToken);

        return new DownloadResult(outcome, localPath, sha256, retrievedAt);
    }

    private static async Task<(string Sha256, long Size)> SaveBodyAsync(
        HttpResponseMessage response,
        string tempPath,
        CvmFile file,
        TimeSpan bodyReadTimeout,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(bodyReadTimeout);

        try
        {
            long size = 0;
            string sha256;
            await using (var target = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            await using (var body = await response.Content.ReadAsStreamAsync(timeout.Token))
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                var buffer = new byte[81_920];
                int read;
                while ((read = await body.ReadAsync(buffer, timeout.Token)) > 0)
                {
                    hash.AppendData(buffer, 0, read);
                    await target.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
                    size += read;
                }

                sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
            }

            if (response.Content.Headers.ContentLength is { } expected && expected != size)
            {
                throw new IOException($"{file.FileName}: CVM announced {expected} bytes but sent {size}.");
            }

            return (sha256, size);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            File.Delete(tempPath);
            throw new TimeoutException($"{file.FileName}: the download took longer than {bodyReadTimeout}.");
        }
        catch
        {
            File.Delete(tempPath);
            throw;
        }
    }
}
