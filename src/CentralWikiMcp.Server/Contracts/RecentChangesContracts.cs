using System.Text.Json.Serialization;

namespace CentralWikiMcp.Server.Contracts;

/// <summary>
/// Одна запись об изменении в ответе <c>wiki_recent_changes</c> (раздел 11.2, Could).
/// </summary>
/// <param name="PageId">Идентификатор страницы.</param>
/// <param name="Path">Логический путь.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Namespace">Раздел wiki.</param>
/// <param name="UpdatedAt">Момент последнего изменения.</param>
/// <param name="Revision">Ревизия содержимого.</param>
public sealed record RecentChangeDto(
    [property: JsonPropertyName("pageId")] string PageId,
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("namespace")] string Namespace,
    [property: JsonPropertyName("updatedAt")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("revision")] string Revision);

/// <summary>
/// Ответ инструмента <c>wiki_recent_changes</c>.
/// </summary>
/// <param name="Changes">Изменённые страницы, отсортированные по убыванию времени.</param>
public sealed record RecentChangesResponseDto(
    [property: JsonPropertyName("changes")] IReadOnlyList<RecentChangeDto> Changes);
