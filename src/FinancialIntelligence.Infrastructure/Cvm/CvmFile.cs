using System.Globalization;

namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>
/// A file on CVM's open-data portal. Built from CVM's fixed naming pattern
/// rather than read from the folder listing: if CVM renames a file, the
/// download fails with a 404 instead of quietly fetching nothing.
/// </summary>
public sealed record CvmFile(string FileName, string UrlPath)
{
    public static CvmFile Cad { get; } = new("cad_cia_aberta.csv", "CIA_ABERTA/CAD/DADOS/cad_cia_aberta.csv");

    public static CvmFile Dfp(int year) => Yearly("DFP", year);

    public static CvmFile Itr(int year) => Yearly("ITR", year);

    private static CvmFile Yearly(string kind, int year)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"{kind.ToLowerInvariant()}_cia_aberta_{year}.zip");
        return new CvmFile(name, $"CIA_ABERTA/DOC/{kind}/DADOS/{name}");
    }
}
