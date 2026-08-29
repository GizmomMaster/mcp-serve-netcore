using System.Globalization;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure;

/// <summary>
/// Проверка состояния синхронизации (FR-34, FR-35).
/// Просроченный или упавший индекс виден в <c>/health/full</c>.
/// </summary>
internal sealed class SyncHealthCheck(
    ISyncStateStore stateStore,
    IWikiSource source,
    IOptions<SyncOptions> syncOptions,
    TimeProvider timeProvider) : IHealthCheck
{
    /// <summary>
    /// Во сколько раз индекс может отстать от интервала синхронизации,
    /// прежде чем состояние признаётся деградировавшим.
    /// </summary>
    private const int StalenessFactor = 3;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var state = await stateStore
            .GetAsync(source.SourceId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
        {
            return HealthCheckResult.Degraded("Синхронизация ещё не запускалась.");
        }

        var data = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["status"] = state.Status.ToString(),
            ["startedAt"] = state.StartedAt.ToString("O", CultureInfo.InvariantCulture),
            ["pagesIndexed"] = state.PagesIndexed,
            ["pagesRemoved"] = state.PagesRemoved,
        };

        if (state.Status == SyncStatus.Failed)
        {
            return HealthCheckResult.Degraded(
                $"Последняя синхронизация завершилась ошибкой: {state.Error}",
                data: data);
        }

        var options = syncOptions.Value;

        if (state.CompletedAt is { } completedAt && options.Enabled)
        {
            var age = timeProvider.GetUtcNow() - completedAt;
            var threshold = TimeSpan.FromMinutes(options.IntervalMinutes * StalenessFactor);

            data["ageMinutes"] = (int)age.TotalMinutes;

            if (age > threshold)
            {
                return HealthCheckResult.Degraded(
                    $"Индекс устарел: последнее обновление {(int)age.TotalMinutes} мин назад.",
                    data: data);
            }
        }

        return HealthCheckResult.Healthy("Синхронизация в норме.", data);
    }
}
