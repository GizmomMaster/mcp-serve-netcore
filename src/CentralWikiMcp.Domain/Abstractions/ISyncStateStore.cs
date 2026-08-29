using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Domain.Abstractions;

/// <summary>
/// Хранилище состояния синхронизации (FR-34, FR-35).
/// </summary>
public interface ISyncStateStore
{
    /// <summary>
    /// Возвращает состояние последней синхронизации источника.
    /// </summary>
    /// <param name="sourceId">Идентификатор источника.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние или <c>null</c>, если синхронизация не запускалась.</returns>
    Task<SyncState?> GetAsync(string sourceId, CancellationToken cancellationToken);

    /// <summary>
    /// Сохраняет состояние синхронизации.
    /// </summary>
    /// <param name="state">Состояние.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача записи.</returns>
    Task SaveAsync(SyncState state, CancellationToken cancellationToken);
}
