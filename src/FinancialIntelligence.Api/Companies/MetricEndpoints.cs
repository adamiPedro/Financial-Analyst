using FinancialIntelligence.Application.FinancialData;
using FinancialIntelligence.Domain.FinancialData;

namespace FinancialIntelligence.Api.Companies;

public sealed record MetricSourceResponse(string AccountCode, string SourceFile, int FilingVersion, DateTimeOffset RetrievedAt);

public sealed record MetricPointResponse(
    DateOnly PeriodEnd,
    string Status,
    decimal? Value,
    bool IsCalculated,
    IReadOnlyList<MetricSourceResponse> Sources);

public sealed record MetricSeriesResponse(
    string Metric,
    string Basis,
    bool NotApplicable,
    IReadOnlyList<MetricPointResponse> Points);

internal static class MetricEndpoints
{
    // Spelled out rather than derived from the enum, so renaming a C# member
    // can't silently change a public URL.
    private static readonly Dictionary<string, Metric> ByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["revenue"] = Metric.Revenue,
        ["gross-profit"] = Metric.GrossProfit,
        ["net-income"] = Metric.NetIncome,
        ["operating-cash-flow"] = Metric.OperatingCashFlow,
        ["total-assets"] = Metric.TotalAssets,
        ["equity"] = Metric.Equity,
    };

    private static readonly Dictionary<Metric, string> NameOf = ByName.ToDictionary(p => p.Value, p => p.Key);

    public static void MapMetricEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/companies/{cvmCode:int}/metrics/{metric}/annual", (
            int cvmCode, string metric, IMetricQuery query, CancellationToken cancellationToken) =>
            SeriesAsync(metric, known => query.GetAnnualAsync(cvmCode, known, cancellationToken)));

        app.MapGet("/api/companies/{cvmCode:int}/metrics/{metric}/quarterly", (
            int cvmCode, string metric, IMetricQuery query, CancellationToken cancellationToken) =>
            SeriesAsync(metric, known => query.GetQuarterlyAsync(cvmCode, known, cancellationToken)));
    }

    private static async Task<IResult> SeriesAsync(string metric, Func<Metric, Task<MetricSeries?>> load)
    {
        if (!ByName.TryGetValue(metric, out var known))
        {
            return Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown metric",
                detail: $"Known metrics: {string.Join(", ", ByName.Keys)}.");
        }

        var series = await load(known);

        return series is null ? Results.NotFound() : Results.Ok(ToResponse(series));
    }

    private static MetricSeriesResponse ToResponse(MetricSeries series) =>
        new(NameOf[series.Metric], series.Basis.ToString(), series.NotApplicable,
            series.Points.Select(p => new MetricPointResponse(
                p.PeriodEnd, p.Status.ToString(), p.Value, p.IsCalculated,
                p.Sources.Select(s => new MetricSourceResponse(s.AccountCode, s.SourceFile, s.FilingVersion, s.RetrievedAt)).ToList()))
            .ToList());
}
