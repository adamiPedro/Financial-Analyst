namespace FinancialIntelligence.Domain.Ingestion;

/// <summary>
/// One time a source file came back with contents (a 200, not a 304).
/// Provenance: a fact's source file and retrieval time point back here, and the
/// SHA-256 says exactly which bytes that was, even after the file on disk has
/// been replaced by a newer copy.
/// </summary>
public sealed class SourceFileDownload
{
    private SourceFileDownload()
    {
        FileName = null!;
        Url = null!;
        Sha256 = null!;
    }

    public SourceFileDownload(
        string fileName,
        string url,
        string? eTag,
        string sha256,
        long sizeBytes,
        DateTimeOffset downloadedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentOutOfRangeException.ThrowIfNegative(sizeBytes);

        // One spelling only, so "same contents?" is a plain string comparison.
        if (sha256 is not { Length: 64 } || !sha256.All(char.IsAsciiHexDigitLower))
        {
            throw new ArgumentException("SHA-256 must be 64 lower-case hex characters.", nameof(sha256));
        }

        FileName = fileName;
        Url = url;
        ETag = string.IsNullOrWhiteSpace(eTag) ? null : eTag;
        Sha256 = sha256;
        SizeBytes = sizeBytes;
        DownloadedAt = downloadedAt;
    }

    public long Id { get; private set; }

    public string FileName { get; private set; }

    public string Url { get; private set; }

    /// <summary>
    /// Kept exactly as the server sent it, weak prefix included, because it is
    /// only ever sent back. Null when the server sent none; the next download is
    /// then a full one, and the SHA-256 still decides whether anything changed.
    /// </summary>
    public string? ETag { get; private set; }

    public string Sha256 { get; private set; }

    public long SizeBytes { get; private set; }

    public DateTimeOffset DownloadedAt { get; private set; }
}
