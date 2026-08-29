namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Состояние последней синхронизации с источником (FR-34, FR-35).
/// </summary>
/// <param name="Source">Идентификатор источника.</param>
/// <param name="StartedAt">Начало последнего запуска.</param>
/// <param name="CompletedAt">Завершение последнего запуска.</param>
/// <param name="Status">Итог запуска.</param>
/// <param name="PagesIndexed">Сколько страниц проиндексировано.</param>
/// <param name="PagesRemoved">Сколько страниц удалено из индекса.</param>
/// <param name="Error">Текст ошибки, если запуск неуспешен.</param>
public sealed record SyncState(
    string Source,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    SyncStatus Status,
    int PagesIndexed,
    int PagesRemoved,
    string? Error);

/// <summary>Итог запуска синхронизации.</summary>
public enum SyncStatus
{
    /// <summary>Ни разу не запускалась.</summary>
    Never = 0,

    /// <summary>Выполняется сейчас.</summary>
    Running = 1,

    /// <summary>Завершена успешно.</summary>
    Succeeded = 2,

    /// <summary>Завершена с ошибкой (FR-35).</summary>
    Failed = 3,
}
