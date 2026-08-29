using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Infrastructure.Persistence;

/// <summary>
/// Состояние синхронизации источника (FR-34).
/// </summary>
internal sealed class SyncStateEntity
{
    /// <summary>Идентификатор источника.</summary>
    public required string Source { get; set; }

    /// <summary>Начало последнего запуска.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>Завершение последнего запуска.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Итог запуска.</summary>
    public SyncStatus Status { get; set; }

    /// <summary>Сколько страниц проиндексировано.</summary>
    public int PagesIndexed { get; set; }

    /// <summary>Сколько страниц удалено.</summary>
    public int PagesRemoved { get; set; }

    /// <summary>Текст ошибки при неуспехе (FR-35).</summary>
    public string? Error { get; set; }
}
