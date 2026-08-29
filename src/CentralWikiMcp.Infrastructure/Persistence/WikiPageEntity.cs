using NpgsqlTypes;

namespace CentralWikiMcp.Infrastructure.Persistence;

/// <summary>
/// Строка индекса wiki в PostgreSQL (NFR-12).
/// </summary>
internal sealed class WikiPageEntity
{
    /// <summary>Идентификатор страницы.</summary>
    public required string PageId { get; set; }

    /// <summary>Логический путь.</summary>
    public required string Path { get; set; }

    /// <summary>Заголовок.</summary>
    public required string Title { get; set; }

    /// <summary>Содержимое в Markdown.</summary>
    public required string ContentMarkdown { get; set; }

    /// <summary>Раздел wiki, по которому применяется ACL.</summary>
    public required string Namespace { get; set; }

    /// <summary>Теги страницы.</summary>
    public string[] Tags { get; set; } = [];

    /// <summary>Момент последнего изменения в источнике.</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>Ревизия содержимого; используется для инкрементальной синхронизации.</summary>
    public required string Revision { get; set; }

    /// <summary>Идентификатор источника.</summary>
    public required string Source { get; set; }

    /// <summary>Момент попадания страницы в индекс.</summary>
    public DateTimeOffset IndexedAt { get; set; }

    /// <summary>
    /// Поисковый вектор, вычисляемый базой (FR-14).
    /// Заголовок весит выше тела, поэтому совпадение в заголовке даёт больший score.
    /// </summary>
    public NpgsqlTsVector? SearchVector { get; set; }
}
