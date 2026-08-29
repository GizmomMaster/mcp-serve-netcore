using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Domain.Abstractions;

/// <summary>
/// Поисковый индекс wiki (FR-10, FR-14, FR-20).
/// Проверка прав здесь не выполняется — она лежит на слое инструментов.
/// </summary>
public interface IWikiIndex
{
    /// <summary>
    /// Полнотекстовый поиск с фильтрами.
    /// </summary>
    /// <param name="query">Параметры запроса.</param>
    /// <param name="allowedNamespaces">Разделы, доступные субъекту; <c>null</c> — без ограничения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результаты, отсортированные по убыванию релевантности.</returns>
    Task<IReadOnlyList<WikiSearchResult>> SearchAsync(
        WikiSearchQuery query,
        IReadOnlyCollection<string>? allowedNamespaces,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает страницу по идентификатору или пути (FR-20).
    /// </summary>
    /// <param name="pageIdOrPath">Идентификатор либо логический путь.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница или <c>null</c>, если не найдена.</returns>
    Task<WikiPage?> GetPageAsync(string pageIdOrPath, CancellationToken cancellationToken);

    /// <summary>
    /// Постранично перечисляет метаданные страниц.
    /// </summary>
    /// <param name="namespaceFilter">Фильтр по разделу wiki.</param>
    /// <param name="allowedNamespaces">Разделы, доступные субъекту; <c>null</c> — без ограничения.</param>
    /// <param name="skip">Сколько записей пропустить.</param>
    /// <param name="take">Сколько записей вернуть.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Метаданные страниц, отсортированные по пути.</returns>
    Task<IReadOnlyList<WikiPageSummary>> ListPagesAsync(
        string? namespaceFilter,
        IReadOnlyCollection<string>? allowedNamespaces,
        int skip,
        int take,
        CancellationToken cancellationToken);

    /// <summary>
    /// Добавляет или обновляет страницы в индексе.
    /// </summary>
    /// <param name="pages">Страницы для сохранения.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сколько страниц записано.</returns>
    Task<int> UpsertAsync(IReadOnlyCollection<WikiPage> pages, CancellationToken cancellationToken);

    /// <summary>
    /// Удаляет из индекса страницы источника, которых нет в переданном наборе идентификаторов.
    /// </summary>
    /// <param name="sourceId">Источник, в пределах которого выполняется чистка.</param>
    /// <param name="keepPageIds">Идентификаторы страниц, которые нужно сохранить.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Сколько страниц удалено.</returns>
    Task<int> RemoveMissingAsync(
        string sourceId,
        IReadOnlyCollection<string> keepPageIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// Возвращает ревизии страниц источника — для инкрементальной синхронизации (FR-36).
    /// </summary>
    /// <param name="sourceId">Идентификатор источника.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Словарь «идентификатор страницы — ревизия».</returns>
    Task<IReadOnlyDictionary<string, string>> GetRevisionsAsync(
        string sourceId,
        CancellationToken cancellationToken);
}
