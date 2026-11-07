using Lacoos.SharedKernel.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Lacoos.Api.Infrastructure;

internal sealed class PostgresHealthCheck(IConfiguration configuration) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(configuration.GetConnectionString(ModulePersistence.ConnectionStringName));
            await connection.OpenAsync(ct);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(ct);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is NpgsqlException or TimeoutException or InvalidOperationException)
        {
            return HealthCheckResult.Unhealthy("Banco de dados indisponível.", exception);
        }
    }
}
