using System.Globalization;
using System.Net;
using FinancialIntelligence.Application.Ingestion;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.Ingestion;
using FinancialIntelligence.IntegrationTests.Cvm;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace FinancialIntelligence.IntegrationTests.Ingestion;

// The clock sits in 2025 and FirstYear is 2024 unless a test says otherwise:
// the 2024 files are a past year, the 2025 files the current one.
public sealed class IngestionSyncTests : IAsyncLifetime, IDisposable
{
    private const string Cad = "cad_cia_aberta.csv";
    private const string Dfp2024 = "dfp_cia_aberta_2024.zip";
    private const string Dfp2025 = "dfp_cia_aberta_2025.zip";
    private const string Itr2024 = "itr_cia_aberta_2024.zip";
    private const string Itr2025 = "itr_cia_aberta_2025.zip";

    // In the CAD fixture and the statement fixtures alike. BB Seguridade (23159)
    // is in the statements but not in CAD, so it can't be tracked here.
    private static readonly int[] Tracked = [9512, 19348, 906];

    // From Fixtures/Cvm/README.md: CAD holds 11 companies; DFP 2024 has a
    // consolidated and an individual filing per company; ITR 2024 has Petrobras's
    // three quarters on both bases.
    private const int CompaniesInCad = 11;
    private const int DfpFilings = 3 * 2;
    private const int ItrFilings = 3 * 2;

    private readonly TestDatabase _database = new();
    private readonly FakeCvm _cvm = new();
    private readonly FixtureZip _zips = new();
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"sync_test_{Guid.NewGuid():N}");
    private readonly List<ServiceProvider> _providers = [];
    private ServiceProvider? _services;

    public Task InitializeAsync() => _database.InitializeAsync();

    public async Task DisposeAsync()
    {
        foreach (var provider in _providers)
        {
            await provider.DisposeAsync();
        }

        await _database.DisposeAsync();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    public void Dispose()
    {
        _cvm.Dispose();
        _zips.Dispose();
    }

    private IIngestionSync Sync(int firstYear = 2024, int[]? tracked = null, int currentYear = 2025)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Cvm:BaseUrl"] = "https://cvm.test/dados/",
            ["Cvm:DataDirectory"] = _folder,
            ["Cvm:BodyReadTimeout"] = "00:00:00.500",
            ["Statements:FirstYear"] = firstYear.ToString(CultureInfo.InvariantCulture),
        };
        var codes = tracked ?? Tracked;
        for (var i = 0; i < codes.Length; i++)
        {
            settings[$"Statements:TrackedCompanies:{i}"] = codes[i].ToString(CultureInfo.InvariantCulture);
        }

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(currentYear, 3, 1, 12, 0, 0, TimeSpan.Zero)));
        services.AddFakeLogging();
        services.AddInfrastructure(_database.ConnectionString);
        services.AddIngestion();
        services.AddHttpClient<CvmDownloader>().ConfigurePrimaryHttpMessageHandler(() => _cvm);
        services.PostConfigure<HttpStandardResilienceOptions>(
            "CvmDownloader-standard", options => options.Retry.Delay = TimeSpan.Zero);

        // A test may build a second provider (different tracked list) over the
        // same database and data folder; all are disposed at the end.
        _services = services.BuildServiceProvider();
        _providers.Add(_services);
        return _services.GetRequiredService<IIngestionSync>();
    }

    private void ServePublishedFiles()
    {
        var cad = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", Cad));
        _cvm.Serve(Cad, () => FakeCvm.Ok(cad, "\"cad-1\""));
        ServeZip(Dfp2024, "dfp_2024");
        ServeZip(Itr2024, "itr_2024");
        ServeStatus(Dfp2025, HttpStatusCode.NotFound);
        ServeStatus(Itr2025, HttpStatusCode.NotFound);
    }

    private byte[] ZipBytes(string fileName, string fixtureFolder) =>
        File.ReadAllBytes(_zips.Create(fixtureFolder, fileName));

    private void ServeZip(string fileName, string fixtureFolder)
    {
        var bytes = ZipBytes(fileName, fixtureFolder);
        _cvm.Serve(fileName, () => FakeCvm.Ok(bytes, $"\"{fileName}-1\""));
    }

    private void ServeStatus(string fileName, HttpStatusCode status) =>
        _cvm.Serve(fileName, () => FakeCvm.Status(status));

    private async Task<(int Companies, int DfpFilings, int ItrFilings, int Facts)> StoredAsync()
    {
        await using var db = _database.CreateContext();
        return (
            await db.Companies.CountAsync(),
            await db.Filings.CountAsync(f => f.Form == FilingForm.Dfp),
            await db.Filings.CountAsync(f => f.Form == FilingForm.Itr),
            await db.FinancialFacts.CountAsync());
    }

    private List<FakeLogRecord> SyncLogs() =>
        _services!.GetFakeLogCollector().GetSnapshot()
            .Where(r => r.Category == typeof(IngestionSync).FullName)
            .ToList();

    [Fact]
    public async Task A_fresh_database_gets_the_company_list_and_every_published_file()
    {
        ServePublishedFiles();

        var result = await Sync().RunAsync(CancellationToken.None);

        Assert.Equal(new IngestionSyncResult(
            FilesImported: 3, FilesNotPublished: 2, FilesFailed: 0, FilingsAdded: DfpFilings + ItrFilings), result);
        var stored = await StoredAsync();
        Assert.Equal((CompaniesInCad, DfpFilings, ItrFilings), (stored.Companies, stored.DfpFilings, stored.ItrFilings));
        Assert.Contains(SyncLogs(), r => r.Level == LogLevel.Information
            && r.Message.StartsWith("Ingestion finished", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_second_run_adds_nothing()
    {
        ServePublishedFiles();
        var sync = Sync();
        await sync.RunAsync(CancellationToken.None);
        var before = await StoredAsync();

        var second = await sync.RunAsync(CancellationToken.None);

        Assert.Equal(new IngestionSyncResult(3, 2, 0, 0), second);
        Assert.Equal(before, await StoredAsync());
    }

    [Fact]
    public async Task A_file_cvm_says_has_not_changed_is_still_imported()
    {
        // The case that rules out "skip the import on a 304": a company added to
        // the tracked list lives in files that haven't changed.
        ServePublishedFiles();
        await Sync(tracked: [9512]).RunAsync(CancellationToken.None);
        ServeStatus(Cad, HttpStatusCode.NotModified);
        ServeStatus(Dfp2024, HttpStatusCode.NotModified);
        ServeStatus(Itr2024, HttpStatusCode.NotModified);

        var result = await Sync(tracked: [9512, 19348]).RunAsync(CancellationToken.None);

        Assert.Equal(2, result.FilingsAdded);
        await using var db = _database.CreateContext();
        Assert.Equal(2, await db.Filings.CountAsync(f => f.CvmCode == 19348));
    }

    [Fact]
    public async Task A_current_year_file_cvm_has_not_published_yet_is_not_a_failure()
    {
        ServePublishedFiles();

        await Sync().RunAsync(CancellationToken.None);

        var logs = SyncLogs();
        Assert.Contains(logs, r => r.Level == LogLevel.Information && r.Message.Contains(Itr2025, StringComparison.Ordinal));
        Assert.DoesNotContain(logs, r => r.Level >= LogLevel.Warning);
    }
}
