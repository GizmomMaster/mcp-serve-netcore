using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Server.Endpoints;

/// <summary>
/// Состояние синхронизации в ответе API (FR-34).
/// </summary>
/// <param name="Source">Источник wiki.</param>
/// <param name="Status">Итог последнего запуска.</param>
/// <param name="StartedAt">Начало запуска.</param>
/// <param name="CompletedAt">Завершение запуска.</param>
/// <param name="PagesIndexed">Сколько страниц обновлено.</param>
/// <param name="PagesRemoved">Сколько страниц удалено.</param>
/// <param name="Error">Текст ошибки при неуспехе (FR-35).</param>
public sealed record SyncStatusResponse(
    string Source,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    int PagesIndexed,
    int PagesRemoved,
    string? Error)
{
    /// <summary>
    /// Преобразует доменное состояние в ответ API.
    /// </summary>
    /// <param name="state">Доменное состояние.</param>
    /// <returns>Ответ API.</returns>
    public static SyncStatusResponse From(SyncState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new SyncStatusResponse(
            state.Source,
            state.Status.ToString(),
            state.StartedAt,
            state.CompletedAt,
            state.PagesIndexed,
            state.PagesRemoved,
            state.Error);
    }
}

/// <summary>
/// Ответ выгрузки аудита (FR-54).
/// </summary>
/// <param name="From">Начало периода.</param>
/// <param name="To">Конец периода.</param>
/// <param name="Count">Число записей в ответе.</param>
/// <param name="Events">События аудита.</param>
public sealed record AuditQueryResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int Count,
    IReadOnlyList<AuditEvent> Events);
