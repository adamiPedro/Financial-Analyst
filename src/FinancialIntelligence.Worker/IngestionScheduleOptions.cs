namespace FinancialIntelligence.Worker;

/// <summary>
/// Its own section, not part of "Statements": the interval is the Worker's
/// business, and reading it from an Infrastructure options class would put an
/// Infrastructure type in the Worker outside Program.cs.
/// </summary>
internal sealed class IngestionScheduleOptions
{
    public const string Section = "Ingestion";

    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(24);
}
