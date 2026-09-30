using FinancialIntelligence.Application.FinancialData;

namespace FinancialIntelligence.Api.Companies;

public sealed record RevenueResponse(
    int Year,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal ValueBrl,
    string Basis,
    int FilingVersion,
    string SourceFile,
    DateTimeOffset RetrievedAt);

internal static class RevenueEndpoints
{
    public static void MapRevenueEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/companies/{cvmCode:int}/revenue", async (
            int cvmCode, IRevenueQuery query, CancellationToken cancellationToken) =>
        {
            var revenue = await query.GetAnnualRevenueAsync(cvmCode, cancellationToken);

            return revenue is null
                ? Results.NotFound()
                : Results.Ok(revenue.Select(r => new RevenueResponse(
                    r.PeriodEnd.Year, r.PeriodStart, r.PeriodEnd, r.Value, r.Basis.ToString(),
                    r.Version, r.SourceFile, r.RetrievedAt)));
        });
    }
}
