using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using FinancialIntelligence.Domain.FinancialData;
using FinancialIntelligence.Infrastructure.Cvm;
using FinancialIntelligence.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinancialIntelligence.Infrastructure.FinancialData;

public sealed record StatementImportResult(int FilingsAdded, int FilingsAlreadyStored, int LayoutsChanged, int UnknownLayouts);

/// <summary>
/// Loads one DFP or ITR ZIP: every account of the five statements we use, for
/// the given companies, exactly as published. Metrics are not stored; they are
/// read through MetricMap when served (docs/decisions/0009).
/// </summary>
public sealed partial class StatementImporter(FinancialIntelligenceDbContext db, ILogger<StatementImporter> logger)
{
    private static readonly string[] Statements = ["BPA", "BPP", "DRE", "DFC_MI", "DFC_MD"];
    private static readonly string[] Bases = ["con", "ind"];

    public async Task<StatementImportResult> ImportAsync(
        string zipPath,
        FilingForm form,
        DateTimeOffset retrievedAt,
        IReadOnlySet<int> cvmCodes,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(zipPath);
        ArgumentNullException.ThrowIfNull(cvmCodes);

        var sourceFile = Path.GetFileName(zipPath);
        var prefix = form == FilingForm.Dfp ? "dfp" : "itr";
        var year = YearOf(sourceFile, prefix, nameof(zipPath));
        var codes = cvmCodes.ToList();

        await EnsureRegisteredAsync(codes, cancellationToken);

        var rows = await ReadAsync(zipPath, prefix, year, cvmCodes, cancellationToken);

        // Tracked, so a corrected layout is written on save without extra code.
        var stored = await db.Filings
            .Where(f => f.Form == form && codes.Contains(f.CvmCode))
            .ToDictionaryAsync(f => (f.CvmCode, f.ReferenceDate, f.Version, f.Basis), cancellationToken);

        var added = 0;
        var alreadyStored = 0;
        var layoutsChanged = 0;
        var unknownLayouts = 0;
        foreach (var filingRows in rows.GroupBy(r => (r.CvmCode, r.ReferenceDate, r.Version, r.Basis)))
        {
            var key = filingRows.Key;
            var layout = LayoutDetector.Detect(filingRows.ToList());
            if (layout == StatementLayout.Unknown)
            {
                unknownLayouts++;
                LogUnknownLayout(logger, key.CvmCode, key.ReferenceDate, key.Basis, sourceFile);
            }

            if (stored.TryGetValue(key, out var existing))
            {
                alreadyStored++;
                if (existing.Layout != layout)
                {
                    existing.ChangeLayout(layout);
                    layoutsChanged++;
                }

                continue;
            }

            var filing = new Filing(key.CvmCode, form, key.ReferenceDate, key.Version, key.Basis, layout, sourceFile, retrievedAt);
            db.Filings.Add(filing);
            db.FinancialFacts.AddRange(WithoutRepeats(filingRows, sourceFile)
                .Select(r => new FinancialFact(filing, r.AccountCode, r.PeriodStart, r.PeriodEnd, r.Value)));
            added++;
        }

        // One SaveChanges is one transaction: if any row fails, nothing from the ZIP is written.
        await db.SaveChangesAsync(cancellationToken);

        return new StatementImportResult(added, alreadyStored, layoutsChanged, unknownLayouts);
    }

    // The entries inside are named by year, and CVM's ZIP name carries it. A name
    // for the other form means the wrong file was passed in.
    private static int YearOf(string fileName, string prefix, string paramName)
    {
        var match = Regex.Match(fileName, $"^{prefix}_cia_aberta_(\\d{{4}})\\.zip$", RegexOptions.CultureInvariant);
        return match.Success
            ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture)
            : throw new ArgumentException($"'{fileName}' is not a CVM {prefix.ToUpperInvariant()} file name.", paramName);
    }

    private async Task EnsureRegisteredAsync(List<int> codes, CancellationToken cancellationToken)
    {
        var known = await db.Companies
            .Where(c => codes.Contains(c.CvmCode))
            .Select(c => c.CvmCode)
            .ToListAsync(cancellationToken);

        var missing = codes.Except(known).Order().ToList();
        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                $"Companies {string.Join(", ", missing)} are not registered. Import the company list (CAD) first.");
        }
    }

    private static async Task<List<StatementRow>> ReadAsync(
        string zipPath, string prefix, int year, IReadOnlySet<int> cvmCodes, CancellationToken cancellationToken)
    {
        using var zip = ZipFile.OpenRead(zipPath);

        var rows = new List<StatementRow>();
        foreach (var statement in Statements)
        {
            foreach (var basis in Bases)
            {
                var name = string.Create(CultureInfo.InvariantCulture, $"{prefix}_cia_aberta_{statement}_{basis}_{year}.csv");

                // Every ZIP since 2010 holds all ten. A missing one means CVM changed
                // the format, and importing the rest would store half a filing.
                var entry = zip.GetEntry(name)
                    ?? throw new InvalidDataException($"{Path.GetFileName(zipPath)} has no {name}.");

                await using var csv = entry.Open();
                try
                {
                    rows.AddRange(await StatementReader.ReadAsync(csv, cvmCodes, cancellationToken));
                }
                catch (InvalidDataException e)
                {
                    throw new InvalidDataException($"{name}: {e.Message}", e);
                }
            }
        }

        return rows;
    }

    // CVM files repeat rows inside a filing, thousands a year from 2021. An exact
    // copy is harmless and kept once. Copies that disagree are dropped: picking
    // one would be a guess, and a metric that needs the account then reports
    // unavailable instead of a number we chose.
    private List<StatementRow> WithoutRepeats(IEnumerable<StatementRow> filingRows, string sourceFile)
    {
        var kept = new List<StatementRow>();
        foreach (var same in filingRows.GroupBy(r => (r.AccountCode, r.PeriodStart, r.PeriodEnd)))
        {
            var first = same.First();
            if (same.All(r => r.Value == first.Value))
            {
                kept.Add(first);
                continue;
            }

            LogConflictingRows(logger, first.CvmCode, first.ReferenceDate, first.Basis, first.AccountCode, first.PeriodEnd, sourceFile);
        }

        return kept;
    }

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Filing {CvmCode} {ReferenceDate} ({Basis}) in {SourceFile} matches no known statement layout. Its facts are stored; no metric will be read from them.")]
    private static partial void LogUnknownLayout(ILogger logger, int cvmCode, DateOnly referenceDate, AccountingBasis basis, string sourceFile);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Filing {CvmCode} {ReferenceDate} ({Basis}) in {SourceFile} gives account {AccountCode} for {PeriodEnd} more than once with different values. None of them is stored.")]
    private static partial void LogConflictingRows(ILogger logger, int cvmCode, DateOnly referenceDate, AccountingBasis basis, string accountCode, DateOnly periodEnd, string sourceFile);
}
