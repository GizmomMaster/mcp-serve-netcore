using CentralWikiMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace CentralWikiMcp.Infrastructure.Health;

/// <summary>
/// Проверка доступности PostgreSQL (FR-08).
/// </summary>
internal sealed class DatabaseHealthCheck(WikiDbContext dbContext) : IHealthCheck
{
    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await dbContext.Database
                .CanConnectAsync(cancellationToken)
                .ConfigureAwait(false);

            return canConnect
                ? HealthCheckResult.Healthy("База данных доступна.")
                : HealthCheckResult.Unhealthy("База данных недоступна.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Текст исключения может содержать строку подключения — наружу не отдаём (NFR-35).
            return HealthCheckResult.Unhealthy("Ошибка подключения к базе данных.", ex);
        }
    }
}
