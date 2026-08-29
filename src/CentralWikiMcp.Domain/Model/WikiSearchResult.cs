namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Один результат поиска (FR-13: включает источник и метаданные).
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Snippet">Фрагмент текста с совпадением.</param>
/// <param name="Score">Релевантность, больше — лучше.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого.</param>
public sealed record WikiSearchResult(
    string PageId,
    string Path,
    string Title,
    string Snippet,
    double Score,
    DateTimeOffset UpdatedAt,
    string Revision);
