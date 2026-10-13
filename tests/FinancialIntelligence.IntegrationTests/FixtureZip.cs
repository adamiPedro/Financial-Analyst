using System.IO.Compression;
using System.Text;

namespace FinancialIntelligence.IntegrationTests;

/// <summary>
/// Rebuilds a CVM ZIP from a fixture folder, under CVM's own file name, so the
/// importer reads what a download would give it. Optionally edits lines on the
/// way in, for the cases the real files don't happen to contain.
/// </summary>
public sealed class FixtureZip : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"fixture_zip_{Guid.NewGuid():N}");

    public string Create(string fixtureFolder, string zipName, Func<string, string, IEnumerable<string>>? edit = null)
    {
        var directory = Path.Combine(_root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, zipName);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Cvm", fixtureFolder)))
        {
            var name = Path.GetFileName(file);
            var lines = File.ReadAllText(file, Encoding.Latin1)
                .Split("\r\n")
                .Where(l => l.Length > 0)
                .SelectMany(l => edit is null ? new[] { l } : edit(name, l));

            using var writer = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.Latin1);
            writer.Write(string.Join("\r\n", lines) + "\r\n");
        }

        return path;
    }

    public static Func<string, string, IEnumerable<string>> OnRow(
        string entryName, string rowStart, string accountCode, Func<string, IEnumerable<string>> change) =>
        (name, line) => name == entryName
            && line.StartsWith(rowStart, StringComparison.Ordinal)
            && line.Contains(";ÚLTIMO;", StringComparison.Ordinal)
            && line.Contains($";{accountCode};", StringComparison.Ordinal)
                ? change(line)
                : [line];

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }
}
