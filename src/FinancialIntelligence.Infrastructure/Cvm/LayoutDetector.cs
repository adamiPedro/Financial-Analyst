using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>
/// Names the layout of one filing from its own rows. No column in CVM's files
/// says which layout a filing uses, and the same company changes layouts
/// between years. Three signals have to agree with a known layout exactly;
/// anything else is Unknown, so a new layout fails loudly instead of being read
/// with the wrong mapping. Checked against every DFP filing 2010–2026.
/// </summary>
public static class LayoutDetector
{
    private enum Kind
    {
        Sales,
        Bank,
        Insurer
    }

    private static readonly Dictionary<(Kind Kind, string NetIncome, string Equity), StatementLayout> Layouts = new()
    {
        [(Kind.Sales, "3.11", "2.03")] = StatementLayout.Industrial,
        [(Kind.Bank, "3.09", "2.08")] = StatementLayout.Bank309,
        [(Kind.Bank, "3.11", "2.07")] = StatementLayout.Bank311,
        [(Kind.Bank, "3.13", "2.05")] = StatementLayout.Bank313,
        [(Kind.Insurer, "3.13", "2.03")] = StatementLayout.Insurer,
    };

    public static StatementLayout Detect(IReadOnlyCollection<StatementRow> filingRows)
    {
        ArgumentNullException.ThrowIfNull(filingRows);

        var revenue = filingRows.FirstOrDefault(r => r.AccountCode == "3.01");
        var kind = revenue is null ? null : KindOf(revenue.AccountName);

        // Net income is the last level-2 line of the income statement; 3.99 is
        // earnings per share, which follows it.
        var netIncome = filingRows
            .Select(r => r.AccountCode)
            .Where(code => IsLevel2(code, '3') && code != "3.99")
            .Max(StringComparer.Ordinal);

        var equity = filingRows
            .Where(r => IsLevel2(r.AccountCode, '2')
                && r.AccountName.StartsWith("Patrimônio Líquido", StringComparison.Ordinal))
            .Select(r => r.AccountCode)
            .Distinct()
            .ToList();

        if (kind is null || netIncome is null || equity.Count != 1)
        {
            return StatementLayout.Unknown;
        }

        return Layouts.GetValueOrDefault((kind.Value, netIncome, equity[0]), StatementLayout.Unknown);
    }

    // What 3.01 is called separates sales from interest income and insurance
    // revenue. The words are matched loosely because filers vary the rest
    // ("de"/"da" Intermediação, "Seguradoras/Resseguradoras").
    private static Kind? KindOf(string name) =>
        name.Contains("Venda", StringComparison.Ordinal) ? Kind.Sales
        : name.Contains("Intermedia", StringComparison.Ordinal) ? Kind.Bank
        : name.Contains("Segurad", StringComparison.Ordinal) ? Kind.Insurer
        : null;

    private static bool IsLevel2(string code, char statement) =>
        code.Length == 4 && code[0] == statement && code[1] == '.'
        && char.IsAsciiDigit(code[2]) && char.IsAsciiDigit(code[3]);
}
