namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Метаданные страницы без содержимого — для списка страниц (FR-таблица wiki_list_pages).
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Namespace">Раздел wiki.</param>
/// <param name="Tags">Теги.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого.</param>
public sealed record WikiPageSummary(
    string PageId,
    string Path,
    string Title,
    string Namespace,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAt,
    string Revision);
