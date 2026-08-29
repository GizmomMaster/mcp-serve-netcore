using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentralWikiMcp.Infrastructure.Sync;

/// <summary>
/// Состояние синхронизации в PostgreSQL (FR-34, FR-35).
/// </summary>
internal sealed class PostgresSyncStateStore(WikiDbContext dbContext) : ISyncStateStore
{
    /// <inheritdoc />
    public async Task<SyncState?> GetAsync(string sourceId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        var entity = await dbContext.SyncStates
            .AsNoTracking()
            .FirstOrDefaultAsync(state => state.Source == sourceId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null
            ? null
            : new SyncState(
                entity.Source,
                entity.StartedAt,
                entity.CompletedAt,
                entity.Status,
                entity.PagesIndexed,
                entity.PagesRemoved,
                entity.Error);
    }

    /// <inheritdoc />
    public async Task SaveAsync(SyncState state, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);

        var entity = await dbContext.SyncStates
            .FirstOrDefaultAsync(existing => existing.Source == state.Source, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new SyncStateEntity { Source = state.Source };
            dbContext.SyncStates.Add(entity);
        }

        entity.StartedAt = state.StartedAt;
        entity.CompletedAt = state.CompletedAt;
        entity.Status = state.Status;
        entity.PagesIndexed = state.PagesIndexed;
        entity.PagesRemoved = state.PagesRemoved;

        // Текст ошибки может быть длинным; колонка ограничена 4096 символами.
        entity.Error = state.Error is { Length: > 4096 }
            ? state.Error[..4096]
            : state.Error;

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
