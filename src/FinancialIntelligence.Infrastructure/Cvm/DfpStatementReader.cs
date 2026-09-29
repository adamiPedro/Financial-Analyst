using System.Globalization;
using System.Text;
using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>
/// One account value from a CVM statement file, with its scale already applied.
/// </summary>
public sealed record StatementValue(
    int CvmCode,
    string AccountCode,
    AccountingBasis Basis,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    int Version,
    decimal Value);

/// <summary>
/// Reads one account for one company out of a DFP/ITR statement CSV
/// (e.g. dfp_cia_aberta_DRE_con_2024.csv).
/// </summary>
public static class DfpStatementReader
{
    // Every file repeats the prior year as a comparative (PENÚLTIMO). Only the
    // current period is a new observation; the comparative belongs to last
    // year's filing, and reading both double-counts every value.
    private const string CurrentPeriod = "ÚLTIMO";

    public static async Task<IReadOnlyList<StatementValue>> ReadAccountAsync(
        Stream csv,
        int cvmCode,
        string accountCode,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);
        ArgumentException.ThrowIfNullOrWhiteSpace(accountCode);

        // CVM publishes Latin-1, not UTF-8. Read as UTF-8, "ÚLTIMO" never
        // matches and the filter above silently drops every row.
        using var reader = new StreamReader(csv, Encoding.Latin1);

        var header = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidDataException("Statement file is empty.");
        var columns = Columns.From(header);

        var values = new List<StatementValue>();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            var fields = line.Split(';');

            // CD_CVM is zero-padded here ("009512") but not in CAD ("9512"), so
            // compare as numbers.
            if (int.Parse(fields[columns.CvmCode], CultureInfo.InvariantCulture) != cvmCode
                || fields[columns.AccountCode] != accountCode
                || fields[columns.PeriodOrder] != CurrentPeriod)
            {
                continue;
            }

            values.Add(new StatementValue(
                cvmCode,
                accountCode,
                ParseBasis(fields[columns.Group]),
                DateOnly.ParseExact(fields[columns.PeriodStart], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                DateOnly.ParseExact(fields[columns.PeriodEnd], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                int.Parse(fields[columns.Version], CultureInfo.InvariantCulture),
                ApplyScale(
                    decimal.Parse(fields[columns.Value], CultureInfo.InvariantCulture),
                    fields[columns.Scale],
                    accountCode)));
        }

        return values;
    }

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
    // files before, and a shifted index would read the wrong field without error.
    private sealed record Columns(
        int CvmCode, int Version, int Group, int Scale, int PeriodOrder,
        int PeriodStart, int PeriodEnd, int AccountCode, int Value)
    {
        public static Columns From(string header)
        {
            var names = header.Split(';');

            int Find(string name)
            {
                var index = Array.IndexOf(names, name);
                return index >= 0
                    ? index
                    : throw new InvalidDataException($"Statement file has no {name} column.");
            }

            return new Columns(
                Find("CD_CVM"), Find("VERSAO"), Find("GRUPO_DFP"), Find("ESCALA_MOEDA"),
                Find("ORDEM_EXERC"), Find("DT_INI_EXERC"), Find("DT_FIM_EXERC"),
                Find("CD_CONTA"), Find("VL_CONTA"));
        }
    }
}
