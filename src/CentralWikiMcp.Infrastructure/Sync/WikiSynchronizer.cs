using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure.Sync;

/// <summary>
/// Синхронизация индекса с источником (FR-30, FR-34, FR-36, FR-37).
/// </summary>
public sealed partial class WikiSynchronizer(
    IWikiSource source,
    IWikiIndex index,
    ISyncStateStore stateStore,
    IOptions<SyncOptions> options,
    TimeProvider timeProvider,
    ILogger<WikiSynchronizer> logger)
{
    private readonly SyncOptions options = options.Value;

    /// <summary>
    /// Выполняет синхронизацию.
    /// </summary>
    /// <param name="fullReindex">
    /// <c>true</c> — переиндексировать все страницы независимо от ревизии (FR-37);
    /// <c>false</c> — обновлять только изменившиеся (FR-36).
    /// </param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итоговое состояние синхронизации.</returns>
    public async Task<SyncState> SynchronizeAsync(
        bool fullReindex,
        CancellationToken cancellationToken)
    {
        var startedAt = timeProvider.GetUtcNow();
        var sourceId = source.SourceId;

        await stateStore.SaveAsync(
            new SyncState(sourceId, startedAt, null, SyncStatus.Running, 0, 0, null),
            cancellationToken).ConfigureAwait(false);

        LogSyncStarted(logger, sourceId, fullReindex);

        try
        {
            // При полной переиндексации (FR-37) ревизии не читаем — переписываем всё.
            IReadOnlyDictionary<string, string> knownRevisions = fullReindex
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : await index
                    .GetRevisionsAsync(sourceId, cancellationToken)
                    .ConfigureAwait(false);

            var seenPageIds = new List<string>();
            var batch = new List<WikiPage>(options.BatchSize);
            var indexed = 0;

            await foreach (var page in source
                .EnumeratePagesAsync(cancellationToken)
                .ConfigureAwait(false))
            {
                seenPageIds.Add(page.PageId);

                // FR-36: не переписываем страницы, содержимое которых не изменилось.
                if (!fullReindex
                    && knownRevisions.TryGetValue(page.PageId, out var knownRevision)
                    && string.Equals(knownRevision, page.Revision, StringComparison.Ordinal))
                {
                    continue;
                }

                batch.Add(page);

                if (batch.Count >= options.BatchSize)
                {
                    indexed += await index.UpsertAsync(batch, cancellationToken).ConfigureAwait(false);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                indexed += await index.UpsertAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            var removed = await index
                .RemoveMissingAsync(sourceId, seenPageIds, cancellationToken)
                .ConfigureAwait(false);

            var completed = new SyncState(
                sourceId,
                startedAt,
                timeProvider.GetUtcNow(),
                SyncStatus.Succeeded,
                indexed,
                removed,
                null);

            await stateStore.SaveAsync(completed, cancellationToken).ConfigureAwait(false);
            LogSyncSucceeded(logger, sourceId, indexed, removed);

            return completed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // FR-35: ошибка должна быть видна и в логах, и в состоянии синхронизации.
            LogSyncFailed(logger, sourceId, ex);

            var failed = new SyncState(
                sourceId,
                startedAt,
                timeProvider.GetUtcNow(),
                SyncStatus.Failed,
                0,
                0,
                ex.Message);

            await stateStore.SaveAsync(failed, CancellationToken.None).ConfigureAwait(false);
            return failed;
        }
    }

    [LoggerMessage(
        EventId = 4000,
        Level = LogLevel.Information,
        Message = "Синхронизация wiki начата: источник={Source}, полная={FullReindex}")]
    private static partial void LogSyncStarted(ILogger logger, string source, bool fullReindex);

    [LoggerMessage(
        EventId = 4001,
        Level = LogLevel.Information,
        Message = "Синхронизация wiki завершена: источник={Source}, обновлено={Indexed}, удалено={Removed}")]
    private static partial void LogSyncSucceeded(
        ILogger logger,
        string source,
        int indexed,
        int removed);

    [LoggerMessage(
        EventId = 4002,
        Level = LogLevel.Error,
        Message = "Синхронизация wiki провалена: источник={Source}")]
    private static partial void LogSyncFailed(ILogger logger, string source, Exception exception);
}
