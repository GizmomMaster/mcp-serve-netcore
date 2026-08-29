using CentralWikiMcp.Infrastructure.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure.Sync;

/// <summary>
/// Фоновая синхронизация индекса по расписанию (FR-31, FR-32, UC-3).
/// </summary>
internal sealed partial class WikiSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<WikiSyncBackgroundService> logger) : BackgroundService
{
    private readonly SyncOptions options = options.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            LogDisabled(logger);
            return;
        }

        if (options.RunOnStartup)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(options.IntervalMinutes),
            timeProvider);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
            {
                await RunOnceAsync(stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // NFR-23: штатная остановка сервиса.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        // Синхронизатор зависит от scoped DbContext, поэтому нужен свой scope.
        await using var scope = scopeFactory.CreateAsyncScope();

        var synchronizer = scope.ServiceProvider.GetRequiredService<WikiSynchronizer>();

        // Сбой синхронизации не должен ронять фоновый сервис: он фиксируется
        // в состоянии и health (FR-35), а следующий тик попробует снова.
        await synchronizer.SynchronizeAsync(fullReindex: false, cancellationToken)
            .ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 4010,
        Level = LogLevel.Warning,
        Message = "Фоновая синхронизация wiki отключена настройкой Sync:Enabled")]
    private static partial void LogDisabled(ILogger logger);
}
