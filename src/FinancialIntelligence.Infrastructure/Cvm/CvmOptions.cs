namespace FinancialIntelligence.Infrastructure.Cvm;

/// <summary>Bound from the optional "Cvm" config section; the defaults work as-is.</summary>
public sealed class CvmOptions
{
    public const string Section = "Cvm";

    /// <summary>A setting mainly so tests can point the client at a fake. Must end in "/".</summary>
    public Uri BaseUrl { get; set; } = new("https://dados.cvm.gov.br/dados/");

    /// <summary>Relative to the process's current directory: the repo root in development.</summary>
    public string DataDirectory { get; set; } = "data/raw";

    /// <summary>
    /// The resilience handler's timeouts end when the response headers arrive;
    /// the body is read after that. Without this cap, a server that stops
    /// sending halfway through a file would hang the download forever.
    /// </summary>
    public TimeSpan BodyReadTimeout { get; set; } = TimeSpan.FromMinutes(15);
}
