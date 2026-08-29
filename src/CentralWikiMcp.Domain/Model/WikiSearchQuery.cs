namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Запрос поиска по wiki (раздел 11.3 спецификации).
/// </summary>
/// <param name="Query">Поисковый запрос.</param>
/// <param name="TopK">Сколько результатов вернуть; ограничивается сервером (FR-12).</param>
/// <param name="Namespace">Фильтр по разделу wiki.</param>
/// <param name="Tags">Фильтр по тегам.</param>
/// <param name="UpdatedAfter">Только страницы, обновлённые после указанного момента.</param>
public sealed record WikiSearchQuery(
    string Query,
    int TopK,
    string? Namespace = null,
    IReadOnlyList<string>? Tags = null,
    DateTimeOffset? UpdatedAfter = null);
