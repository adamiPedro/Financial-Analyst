using System.Globalization;
using System.Text;
using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>One current-period row of a CVM statement file, scale already applied.</summary>
public sealed record StatementRow(
    int CvmCode,
    DateOnly ReferenceDate,
    int Version,
    AccountingBasis Basis,
    string AccountCode,
    string AccountName,
    DateOnly? PeriodStart,
    DateOnly PeriodEnd,
    decimal Value);

/// <summary>
/// Reads one DFP/ITR statement CSV (e.g. dfp_cia_aberta_BPA_con_2024.csv) for
/// the given companies.
/// </summary>
public static class StatementReader
{
    // Every file repeats the prior period as a comparative (PENÚLTIMO). Only the
    // current period is a new observation; the comparative belongs to last
    // period's filing, and reading both double-counts every value.
    private const string CurrentPeriod = "ÚLTIMO";

    public static async Task<IReadOnlyList<StatementRow>> ReadAsync(
        Stream csv,
        IReadOnlySet<int> cvmCodes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentNullException.ThrowIfNull(cvmCodes);

        // CVM publishes Latin-1, not UTF-8. Read as UTF-8, "ÚLTIMO" never
        // matches and the filter above silently drops every row.
        using var reader = new StreamReader(csv, Encoding.Latin1);

        var header = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrEmpty(header))
        {
            throw new InvalidDataException("Statement file is empty.");
        }

        var columns = Columns.From(header);

        var rows = new List<StatementRow>();
        var lineNumber = 1;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }

            // CVM does not quote fields. A ';' inside a value would shift every
            // column after it, so a wrong count is an error, not something to read.
            var fields = line.Split(';');
            if (fields.Length != columns.Count)
            {
                throw new InvalidDataException(
                    $"Line {lineNumber} has {fields.Length} fields; the header has {columns.Count}.");
            }

            try
            {
                // CD_CVM is zero-padded here ("009512") but not in CAD ("9512"),
                // so compare as numbers.
                var cvmCode = int.Parse(fields[columns.CvmCode], CultureInfo.InvariantCulture);
                if (!cvmCodes.Contains(cvmCode) || fields[columns.PeriodOrder] != CurrentPeriod)
                {
                    continue;
                }

                rows.Add(Parse(fields, columns, cvmCode));
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidDataException)
            {
                throw new InvalidDataException($"Line {lineNumber}: {e.Message}", e);
            }
        }

        return rows;
    }

    private static StatementRow Parse(string[] fields, Columns columns, int cvmCode)
    {
        var accountCode = fields[columns.AccountCode];

        return new StatementRow(
            cvmCode,
            ParseDate(fields[columns.ReferenceDate]),
            int.Parse(fields[columns.Version], CultureInfo.InvariantCulture),
            ParseBasis(fields[columns.Group]),
            accountCode,
            fields[columns.AccountName],
            columns.PeriodStart is { } start ? ParseDate(fields[start]) : null,
            ParseDate(fields[columns.PeriodEnd]),
            ApplyScale(
                decimal.Parse(fields[columns.Value], CultureInfo.InvariantCulture),
                fields[columns.Scale],
                accountCode));
    }

    private static DateOnly ParseDate(string value) =>
        DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static decimal ApplyScale(decimal value, string scale, string accountCode)
    {
        // Earnings per share (3.99.x) sits in the same statement and carries the
        // statement's scale label, but the values are reais per share. Scaling
        // them turns R$ 2.84 into R$ 2,840.
        if (accountCode.StartsWith("3.99", StringComparison.Ordinal))
        {
            return value;
        }

        return scale switch
        {
            "MIL" => value * 1000m,
            "UNIDADE" => value,
            // Guessing wrong here is a silent 1000x error, so fail instead.
            _ => throw new InvalidDataException($"Unknown ESCALA_MOEDA '{scale}'.")
        };
    }

    private static AccountingBasis ParseBasis(string group) =>
        group.StartsWith("DF Consolidado", StringComparison.Ordinal) ? AccountingBasis.Consolidated
        : group.StartsWith("DF Individual", StringComparison.Ordinal) ? AccountingBasis.Individual
        : throw new InvalidDataException($"Unknown GRUPO_DFP '{group}'.");

    // Looked up by name rather than position: CVM has added columns to these
    // files before, and the balance sheets lack DT_INI_EXERC altogether, so the
    // same position means different columns in different files.
    private sealed record Columns(
        int Count, int CvmCode, int ReferenceDate, int Version, int Group, int Scale, int PeriodOrder,
        int? PeriodStart, int PeriodEnd, int AccountCode, int AccountName, int Value)
    {
        public static Columns From(string header)
        {
            var names = header.Split(';');

            int? Optional(string name)
            {
                var index = Array.IndexOf(names, name);
                return index >= 0 ? index : null;
            }

            int Find(string name) =>
                Optional(name) ?? throw new InvalidDataException($"Statement file has no {name} column.");

            return new Columns(
                names.Length,
                Find("CD_CVM"), Find("DT_REFER"), Find("VERSAO"), Find("GRUPO_DFP"), Find("ESCALA_MOEDA"),
                Find("ORDEM_EXERC"),
                // A balance is a position on one date, so BPA and BPP have no start.
                Optional("DT_INI_EXERC"),
                Find("DT_FIM_EXERC"), Find("CD_CONTA"), Find("DS_CONTA"), Find("VL_CONTA"));
        }
    }
}
