using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace FinancialIntelligence.Api.Health;

/// <summary>
/// Confirms the API can actually reach Postgres, not merely that it is configured.
/// Written by hand rather than pulling in AspNetCore.HealthChecks.NpgSql: one
/// fewer dependency, and the whole check is six lines.
/// </summary>
internal sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1");
            _ = await command.ExecuteScalarAsync(cancellationToken);
            return HealthCheckResult.Healthy("Postgres reachable.");
        }
        catch (NpgsqlException ex)
        {
            // Deliberately does not surface the connection string or inner detail.
            return HealthCheckResult.Unhealthy("Postgres unreachable.", ex);
        }
    }
}
