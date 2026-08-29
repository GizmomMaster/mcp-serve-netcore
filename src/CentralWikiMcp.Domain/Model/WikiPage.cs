namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Страница wiki целиком (FR-20, FR-22, FR-23).
/// </summary>
/// <param name="PageId">Стабильный идентификатор страницы.</param>
/// <param name="Path">Логический путь вида <c>runbooks/deploy</c>.</param>
/// <param name="Title">Заголовок страницы.</param>
/// <param name="ContentMarkdown">Содержимое в Markdown.</param>
/// <param name="Namespace">Раздел wiki, на который навешивается ACL (FR-44).</param>
/// <param name="Tags">Теги страницы.</param>
/// <param name="UpdatedAt">Момент последнего изменения в источнике.</param>
/// <param name="Revision">Ревизия содержимого (FR-23).</param>
/// <param name="Source">Идентификатор источника, откуда получена страница.</param>
public sealed record WikiPage(
    string PageId,
    string Path,
    string Title,
    string ContentMarkdown,
    string Namespace,
    IReadOnlyList<string> Tags,
    DateTimeOffset UpdatedAt,
    string Revision,
    string Source);
