using System.Text.Json.Serialization;

namespace CentralWikiMcp.Server.Contracts;

/// <summary>
/// Ответ инструмента <c>wiki_get_page</c> (раздел 11.4).
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="ContentMarkdown">Содержимое в Markdown, возможно усечённое.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого (FR-23).</param>
/// <param name="Source">Источник страницы.</param>
/// <param name="Truncated">Признак частичной выдачи (FR-24).</param>
/// <param name="Offset">Смещение возвращённого фрагмента в символах.</param>
/// <param name="TotalChars">Полная длина страницы в символах.</param>
/// <param name="NextOffset">Смещение для следующего запроса или <c>null</c>, если это конец.</param>
public sealed record PageResponseDto(
    [property: JsonPropertyName("pageId")] string PageId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("contentMarkdown")] string ContentMarkdown,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("revision")] string Revision,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("truncated")] bool Truncated,
    [property: JsonPropertyName("offset")] int Offset,
    [property: JsonPropertyName("totalChars")] int TotalChars,
    [property: JsonPropertyName("nextOffset")] int? NextOffset);

/// <summary>
/// Метаданные страницы в ответе <c>wiki_list_pages</c>.
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Namespace">Раздел wiki.</param>
/// <param name="Tags">Теги.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого.</param>
public sealed record PageSummaryDto(
    [property: JsonPropertyName("pageId")] string PageId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("tags")] IReadOnlyList<string> Tags,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("revision")] string Revision);

/// <summary>
/// Ответ инструмента <c>wiki_list_pages</c>.
/// </summary>
/// <param name="Pages">Страницы текущей порции.</param>
/// <param name="Skip">Сколько записей пропущено.</param>
/// <param name="Take">Сколько записей запрошено.</param>
/// <param name="HasMore">Есть ли ещё страницы за этой порцией.</param>
public sealed record PageListResponseDto(
    [property: JsonPropertyName("pages")] IReadOnlyList<PageSummaryDto> Pages,
    [property: JsonPropertyName("skip")] int Skip,
    [property: JsonPropertyName("take")] int Take,
    [property: JsonPropertyName("hasMore")] bool HasMore);
