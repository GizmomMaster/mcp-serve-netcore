using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Domain.Abstractions;

/// <summary>
/// Приёмник событий аудита (FR-53). Отдельно от прикладных логов,
/// чтобы аудит можно было выгружать во внешнюю систему (FR-54).
/// </summary>
public interface IAuditSink
{
    /// <summary>
    /// Сохраняет событие аудита.
    /// </summary>
    /// <param name="auditEvent">Событие.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача записи.</returns>
    Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает события аудита за период — для аудитора (раздел 8.4) и выгрузки (FR-54).
    /// </summary>
    /// <param name="fromUtc">Начало периода.</param>
    /// <param name="toUtc">Конец периода.</param>
    /// <param name="take">Максимум записей.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>События, от новых к старым.</returns>
    Task<IReadOnlyList<AuditEvent>> QueryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take,
        CancellationToken cancellationToken);
}
