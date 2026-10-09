using System.Net;
using System.Security.Cryptography;
using System.Text;
using FinancialIntelligence.Domain.Ingestion;
using FinancialIntelligence.Infrastructure;
using FinancialIntelligence.Infrastructure.Cvm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace FinancialIntelligence.IntegrationTests.Cvm;

public sealed class CvmDownloaderTests : IAsyncLifetime, IDisposable
{
    private const string CadUrl = "https://cvm.test/dados/CIA_ABERTA/CAD/DADOS/cad_cia_aberta.csv";
    private const string FirstETag = "\"6ac323d4-16cb49\"";
    private static readonly byte[] Contents = Encoding.Latin1.GetBytes("CNPJ_CIA;DENOM_SOCIAL\r\nversion one\r\n");
    private static readonly byte[] NewContents = Encoding.Latin1.GetBytes("CNPJ_CIA;DENOM_SOCIAL\r\nversion two\r\n");

    private readonly TestDatabase _database = new();
    private readonly FakeCvm _cvm = new();

    // Not created here: the downloader must create it on first use.
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"cvm_test_{Guid.NewGuid():N}");
    private ServiceProvider _services = null!;

    private string LocalPath => Path.Combine(_folder, "cad_cia_aberta.csv");

    public async Task InitializeAsync()
    {
        await _database.InitializeAsync();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cvm:BaseUrl"] = "https://cvm.test/dados/",
                ["Cvm:DataDirectory"] = _folder,
                ["Cvm:BodyReadTimeout"] = "00:00:00.500",
            })
            .Build());
        services.AddInfrastructure(_database.ConnectionString);

        // Only the bottom layer is swapped; the resilience handler above it is
        // the real one. Retry delays are zeroed so the retry test runs instantly.
        services.AddHttpClient<CvmDownloader>().ConfigurePrimaryHttpMessageHandler(() => _cvm);
        services.PostConfigure<HttpStandardResilienceOptions>(
            "CvmDownloader-standard", options => options.Retry.Delay = TimeSpan.Zero);

        _services = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _database.DisposeAsync();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    // xunit's IAsyncLifetime.DisposeAsync is not IAsyncDisposable, so CA1001
    // only accepts the owned handler being released through IDisposable.
    public void Dispose() => _cvm.Dispose();

    private async Task<DownloadResult> DownloadAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = _services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<CvmDownloader>()
            .DownloadAsync(CvmFile.Cad, cancellationToken);
    }

    private async Task<List<SourceFileDownload>> RecordsAsync()
    {
        await using var db = _database.CreateContext();
        return await db.SourceFileDownloads.OrderBy(d => d.Id).ToListAsync();
    }

    private static string Sha(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private async Task DownloadFirstCopyAsync()
    {
        _cvm.Reply(() => FakeCvm.Ok(Contents, FirstETag));
        await DownloadAsync();
    }

    private async Task AssertFirstCopyIsKeptAsync()
    {
        Assert.Equal(Contents, await File.ReadAllBytesAsync(LocalPath));
        Assert.False(File.Exists(LocalPath + ".download"));
        Assert.Single(await RecordsAsync());
    }

    [Fact]
    public async Task The_first_download_saves_the_file_and_records_it()
    {
        _cvm.Reply(() => FakeCvm.Ok(Contents, FirstETag));

        var result = await DownloadAsync();

        Assert.Equal(DownloadOutcome.Updated, result.Outcome);
        Assert.Equal(LocalPath, result.LocalPath);
        Assert.Equal(Sha(Contents), result.Sha256);
        Assert.Equal(Contents, await File.ReadAllBytesAsync(LocalPath));
        Assert.Equal(new Uri(CadUrl), Assert.Single(_cvm.Requests).Url);

        var record = Assert.Single(await RecordsAsync());
        Assert.Equal("cad_cia_aberta.csv", record.FileName);
        Assert.Equal(CadUrl, record.Url);
        Assert.Equal(FirstETag, record.ETag);
        Assert.Equal(Sha(Contents), record.Sha256);
        Assert.Equal(Contents.Length, record.SizeBytes);
    }

    [Fact]
    public async Task A_file_cvm_says_has_not_changed_is_not_downloaded_again()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.NotModified));

        var result = await DownloadAsync();

        Assert.Equal(DownloadOutcome.NotModified, result.Outcome);
        Assert.Equal(Sha(Contents), result.Sha256);
        Assert.Equal(FirstETag, _cvm.Requests[1].IfNoneMatch);
        await AssertFirstCopyIsKeptAsync();
    }

    [Fact]
    public async Task A_weak_etag_is_sent_back_exactly_as_received()
    {
        _cvm.Reply(() => FakeCvm.Ok(Contents, "W/\"weak-1\""));
        await DownloadAsync();
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.NotModified));

        await DownloadAsync();

        Assert.Equal("W/\"weak-1\"", _cvm.Requests[1].IfNoneMatch);
    }

    [Fact]
    public async Task A_rebuilt_file_with_the_same_bytes_is_unchanged()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() => FakeCvm.Ok(Contents, "\"rebuilt\""));

        var result = await DownloadAsync();

        Assert.Equal(DownloadOutcome.Unchanged, result.Outcome);
        Assert.Equal(Contents, await File.ReadAllBytesAsync(LocalPath));
        Assert.False(File.Exists(LocalPath + ".download"));
        Assert.Equal("\"rebuilt\"", (await RecordsAsync())[^1].ETag);
    }

    [Fact]
    public async Task New_bytes_replace_the_file()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() => FakeCvm.Ok(NewContents, "\"v2\""));

        var result = await DownloadAsync();

        Assert.Equal(DownloadOutcome.Updated, result.Outcome);
        Assert.Equal(Sha(NewContents), result.Sha256);
        Assert.Equal(NewContents, await File.ReadAllBytesAsync(LocalPath));
        Assert.Equal(2, (await RecordsAsync()).Count);
    }

    [Fact]
    public async Task A_file_deleted_from_disk_is_downloaded_in_full()
    {
        await DownloadFirstCopyAsync();
        File.Delete(LocalPath);
        _cvm.Reply(() => FakeCvm.Ok(Contents, FirstETag));

        var result = await DownloadAsync();

        Assert.Null(_cvm.Requests[1].IfNoneMatch);
        Assert.Equal(DownloadOutcome.Updated, result.Outcome);
        Assert.Equal(Contents, await File.ReadAllBytesAsync(LocalPath));
    }

    [Fact]
    public async Task A_connection_dropped_mid_file_keeps_the_old_copy()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(TroubledStream.Dropping(NewContents[..10])),
        });

        await Assert.ThrowsAsync<IOException>(() => DownloadAsync());

        await AssertFirstCopyIsKeptAsync();
    }

    [Fact]
    public async Task A_body_shorter_than_its_content_length_keeps_the_old_copy()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() =>
        {
            var response = FakeCvm.Ok(NewContents, "\"v2\"");
            response.Content.Headers.ContentLength = NewContents.Length + 10;
            return response;
        });

        await Assert.ThrowsAsync<IOException>(() => DownloadAsync());

        await AssertFirstCopyIsKeptAsync();
    }

    [Fact]
    public async Task A_stalled_download_times_out_and_keeps_the_old_copy()
    {
        await DownloadFirstCopyAsync();
        _cvm.Reply(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(TroubledStream.Stalling(NewContents[..10])),
        });

        await Assert.ThrowsAsync<TimeoutException>(() => DownloadAsync());

        await AssertFirstCopyIsKeptAsync();
    }

    [Fact]
    public async Task A_cancelled_download_keeps_the_old_copy()
    {
        await DownloadFirstCopyAsync();
        // No timer: the stalled body cancels it, so the cancel lands mid-read.
        using var cancel = new CancellationTokenSource();
        _cvm.Reply(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(TroubledStream.Stalling(NewContents[..10], onStall: () => cancel.Cancel())),
        });

        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => DownloadAsync(cancel.Token));

        Assert.IsNotType<TimeoutException>(error);

        await AssertFirstCopyIsKeptAsync();
    }

    [Fact]
    public async Task Server_errors_are_retried()
    {
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.ServiceUnavailable));
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.ServiceUnavailable));
        _cvm.Reply(() => FakeCvm.Ok(Contents, FirstETag));

        var result = await DownloadAsync();

        Assert.Equal(DownloadOutcome.Updated, result.Outcome);
        Assert.Equal(3, _cvm.Requests.Count);
    }

    [Fact]
    public async Task A_missing_file_fails_at_once_without_retrying()
    {
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.NotFound));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => DownloadAsync());

        Assert.Contains(CadUrl, error.Message, StringComparison.Ordinal);
        Assert.Single(_cvm.Requests);
        Assert.Empty(await RecordsAsync());
    }

    [Fact]
    public async Task A_not_modified_answer_to_a_plain_request_is_an_error()
    {
        await DownloadFirstCopyAsync();
        File.Delete(LocalPath);
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.NotModified));

        var error = await Assert.ThrowsAsync<HttpRequestException>(() => DownloadAsync());

        Assert.Contains(CadUrl, error.Message, StringComparison.Ordinal);
        Assert.Null(_cvm.Requests[1].IfNoneMatch);
        Assert.Single(await RecordsAsync());
    }

    [Fact]
    public async Task A_not_modified_answer_with_nothing_stored_is_an_error()
    {
        _cvm.Reply(() => FakeCvm.Status(HttpStatusCode.NotModified));

        await Assert.ThrowsAsync<HttpRequestException>(() => DownloadAsync());

        Assert.Empty(await RecordsAsync());
    }
}
