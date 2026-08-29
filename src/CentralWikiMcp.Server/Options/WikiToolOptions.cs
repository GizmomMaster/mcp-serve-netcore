using System.ComponentModel.DataAnnotations;

namespace CentralWikiMcp.Server.Options;

/// <summary>
/// Серверные лимиты выдачи (FR-12, FR-24, FR-61, FR-62, FR-63).
/// </summary>
public sealed class WikiToolOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "WikiTools";

    /// <summary>Значение <c>top_k</c> по умолчанию (раздел 11.3).</summary>
    [Range(1, 100)]
    public int DefaultTopK { get; set; } = 5;

    /// <summary>Жёсткий серверный потолок <c>top_k</c> (FR-12, FR-62).</summary>
    [Range(1, 100)]
    public int MaxTopK { get; set; } = 25;

    /// <summary>Максимальный размер ответа в символах (FR-61).</summary>
    [Range(1000, 1_000_000)]
    public int MaxResponseChars { get; set; } = 100_000;

    /// <summary>Размер одной порции при частичной выдаче страницы (FR-24).</summary>
    [Range(1000, 1_000_000)]
    public int PageChunkChars { get; set; } = 20_000;

    /// <summary>Таймаут поискового запроса, мс (FR-63).</summary>
    [Range(100, 60_000)]
    public int SearchTimeoutMs { get; set; } = 3000;

    /// <summary>Максимальное число страниц в одном ответе <c>wiki_list_pages</c>.</summary>
    [Range(1, 1000)]
    public int MaxListPageSize { get; set; } = 100;

    /// <summary>Максимальное число записей в ответе <c>wiki_recent_changes</c> (Could).</summary>
    [Range(1, 1000)]
    public int MaxRecentChanges { get; set; } = 50;
}
