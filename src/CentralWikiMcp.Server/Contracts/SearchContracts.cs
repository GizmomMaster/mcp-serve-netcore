using System.Text.Json.Serialization;

namespace CentralWikiMcp.Server.Contracts;

/// <summary>
/// Фильтры поиска (раздел 11.3).
/// </summary>
public sealed class WikiSearchFilters
{
    /// <summary>Раздел wiki.</summary>
    [JsonPropertyName("namespace")]
    public string? Namespace { get; set; }

    /// <summary>Фильтр по тегам: страница должна содержать все указанные теги.</summary>
    [JsonPropertyName("tags")]
    public IList<string>? Tags { get; set; }

    /// <summary>Только страницы, обновлённые после указанного момента.</summary>
    [JsonPropertyName("updated_after")]
    public DateTimeOffset? UpdatedAfter { get; set; }
}

/// <summary>
/// Один результат поиска в ответе инструмента (раздел 11.3).
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Snippet">Фрагмент с совпадением.</param>
/// <param name="Score">Релевантность.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого.</param>
public sealed record SearchResultDto(
    [property: JsonPropertyName("pageId")] string PageId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("snippet")] string Snippet,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("revision")] string Revision);

/// <summary>
/// Ответ инструмента <c>wiki_search</c> (раздел 11.3).
/// </summary>
/// <param name="Results">Найденные страницы.</param>
public sealed record SearchResponseDto(
    [property: JsonPropertyName("results")] IReadOnlyList<SearchResultDto> Results);
