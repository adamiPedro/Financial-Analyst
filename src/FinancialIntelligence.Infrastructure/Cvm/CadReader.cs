using System.Globalization;
using System.Text;
using FinancialIntelligence.Domain.Companies;

namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>
/// One company from CVM's register, after its per-market rows are merged.
/// </summary>
public sealed record CadCompany(int CvmCode, Cnpj Cnpj, string Name, string? Sector, string? Status);

/// <summary>
/// Reads CVM's company register (cad_cia_aberta.csv). Cancelled companies are
/// included, which is what lets their historical filings be imported.
/// </summary>
public static class CadReader
{
    public static async Task<IReadOnlyList<CadCompany>> ReadAsync(Stream csv, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(csv);

        // Latin-1, like every CVM file. Read as UTF-8, accented names and
        // statuses come back garbled without any error.
        using var reader = new StreamReader(csv, Encoding.Latin1);

        var header = await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidDataException("Company file is empty.");
        var columns = Columns.From(header);

        var companies = new Dictionary<int, CadCompany>();
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

            var company = Parse(fields, columns, lineNumber);

            // A company trading on more than one market gets one row per market
            // (TP_MERC). Only the fields we store are compared: a difference in
            // one of those means CVM published two versions of the company, and
            // picking one is a guess. A difference in a phone number or address
            // we never keep shouldn't stop the whole import.
            if (companies.TryGetValue(company.CvmCode, out var first))
            {
                if (first != company)
                {
                    throw new InvalidDataException(
                        $"Line {lineNumber}: CVM code {company.CvmCode} appears again with different details. " +
                        $"First: {Describe(first)}. This line: {Describe(company)}.");
                }

                continue;
            }

            companies.Add(company.CvmCode, company);
        }

        return companies.Values.ToList();
    }

    private static CadCompany Parse(string[] fields, Columns columns, int lineNumber)
    {
        var code = fields[columns.CvmCode];
        if (!int.TryParse(code, NumberStyles.None, CultureInfo.InvariantCulture, out var cvmCode) || cvmCode <= 0)
        {
            throw new InvalidDataException($"Line {lineNumber}: CD_CVM '{code}' is not a CVM code.");
        }

        if (!Cnpj.TryParse(fields[columns.Cnpj], out var cnpj))
        {
            throw new InvalidDataException(
                $"Line {lineNumber}, CVM code {cvmCode}: '{fields[columns.Cnpj]}' is not a valid CNPJ.");
        }

        var name = fields[columns.Name];
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidDataException($"Line {lineNumber}, CVM code {cvmCode}: the name is blank.");
        }

        return new CadCompany(cvmCode, cnpj, name.Trim(), Blank(fields[columns.Sector]), Blank(fields[columns.Status]));
    }

    private static string Describe(CadCompany company) =>
        $"{company.Cnpj.Formatted}, {company.Name}, {company.Sector ?? "(no sector)"}, {company.Status ?? "(no status)"}";

    private static string? Blank(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record Columns(int Cnpj, int Name, int Status, int CvmCode, int Sector, int Count)
    {
        public static Columns From(string header)
        {
            var names = header.Split(';');

            int Find(string name)
            {
                var index = Array.IndexOf(names, name);
                return index >= 0
                    ? index
                    : throw new InvalidDataException($"Company file has no {name} column.");
            }

            return new Columns(
                Find("CNPJ_CIA"), Find("DENOM_SOCIAL"), Find("SIT"), Find("CD_CVM"),
                Find("SETOR_ATIV"), names.Length);
        }
    }
}
