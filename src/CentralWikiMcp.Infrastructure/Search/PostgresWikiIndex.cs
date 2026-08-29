using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentralWikiMcp.Infrastructure.Search;

/// <summary>
/// Полнотекстовый индекс wiki на PostgreSQL (FR-10, FR-14, FR-20, NFR-12).
/// </summary>
internal sealed class PostgresWikiIndex(WikiDbContext dbContext, TimeProvider timeProvider)
    : IWikiIndex
{
    /// <summary>Длина возвращаемого фрагмента с совпадением.</summary>
    private const int SnippetLength = 300;

    /// <summary>
    /// Сколько символов содержимого тянуть из БД, чтобы построить фрагмент.
    /// Полный текст не выгружаем: страницы бывают крупными, а поиск обязан
    /// укладываться в p95 &lt; 500 мс (раздел 12.2).
    /// </summary>
    private const int SnippetSourceLength = 2000;

    /// <inheritdoc />
    public async Task<IReadOnlyList<WikiSearchResult>> SearchAsync(
        WikiSearchQuery query,
        IReadOnlyCollection<string>? allowedNamespaces,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Пустой список разрешённых разделов означает отсутствие доступа (FR-11).
        if (allowedNamespaces is { Count: 0 })
        {
            return [];
        }

        // Вызовы EF.Functions должны стоять внутри самого выражения запроса:
        // вынесенный в переменную tsquery заставляет EF уйти в клиентское вычисление.
        var searchText = query.Query;

        var pages = ApplyFilters(dbContext.Pages.AsNoTracking(), query, allowedNamespaces)
            .Where(page => page.SearchVector!.Matches(
                EF.Functions.WebSearchToTsQuery(
                    WikiDbContext.TextSearchConfiguration,
                    searchText)));

        var ranked = await pages
            .Select(page => new
            {
                page.PageId,
                page.Path,
                page.Title,
                page.UpdatedAt,
                page.Revision,
                Rank = page.SearchVector!.Rank(
                    EF.Functions.WebSearchToTsQuery(
                        WikiDbContext.TextSearchConfiguration,
                        searchText)),
                Head = page.ContentMarkdown.Substring(0, SnippetSourceLength),
            })
            .OrderByDescending(page => page.Rank)
            .ThenByDescending(page => page.UpdatedAt)
            .Take(query.TopK)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return [.. ranked.Select(page => new WikiSearchResult(
            page.PageId,
            page.Path,
            page.Title,
            BuildSnippet(page.Head, query.Query),
            page.Rank,
            page.UpdatedAt,
            page.Revision))];
    }

    /// <inheritdoc />
    public async Task<WikiPage?> GetPageAsync(
        string pageIdOrPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pageIdOrPath);

        var normalizedPath = pageIdOrPath.Trim().TrimStart('/');

        var entity = await dbContext.Pages
            .AsNoTracking()
            .FirstOrDefaultAsync(
                page => page.PageId == pageIdOrPath || page.Path == normalizedPath,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<WikiPageSummary>> ListPagesAsync(
        string? namespaceFilter,
        IReadOnlyCollection<string>? allowedNamespaces,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        if (allowedNamespaces is { Count: 0 })
        {
            return [];
        }

        var pages = dbContext.Pages.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(namespaceFilter))
        {
            pages = pages.Where(page => page.Namespace == namespaceFilter);
        }

        if (allowedNamespaces is not null)
        {
            pages = pages.Where(page => allowedNamespaces.Contains(page.Namespace));
        }

        var summaries = await pages
            .OrderBy(page => page.Path)
            .Skip(skip)
            .Take(take)
            .Select(page => new WikiPageSummary(
                page.PageId,
                page.Path,
                page.Title,
                page.Namespace,
                page.Tags,
                page.UpdatedAt,
                page.Revision))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return summaries;
    }

    /// <inheritdoc />
    public async Task<int> UpsertAsync(
        IReadOnlyCollection<WikiPage> pages,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count == 0)
        {
            return 0;
        }

        var now = timeProvider.GetUtcNow();
        var ids = pages.Select(page => page.PageId).ToArray();

        var existing = await dbContext.Pages
            .Where(page => ids.Contains(page.PageId))
            .ToDictionaryAsync(page => page.PageId, cancellationToken)
            .ConfigureAwait(false);

        foreach (var page in pages)
        {
            if (existing.TryGetValue(page.PageId, out var entity))
            {
                entity.Path = page.Path;
                entity.Title = page.Title;
                entity.ContentMarkdown = page.ContentMarkdown;
                entity.Namespace = page.Namespace;
                entity.Tags = [.. page.Tags];
                entity.UpdatedAt = page.UpdatedAt;
                entity.Revision = page.Revision;
                entity.Source = page.Source;
                entity.IndexedAt = now;
            }
            else
            {
                dbContext.Pages.Add(new WikiPageEntity
                {
                    PageId = page.PageId,
                    Path = page.Path,
                    Title = page.Title,
                    ContentMarkdown = page.ContentMarkdown,
                    Namespace = page.Namespace,
                    Tags = [.. page.Tags],
                    UpdatedAt = page.UpdatedAt,
                    Revision = page.Revision,
                    Source = page.Source,
                    IndexedAt = now,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return pages.Count;
    }

    /// <inheritdoc />
    public async Task<int> RemoveMissingAsync(
        string sourceId,
        IReadOnlyCollection<string> keepPageIds,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);
        ArgumentNullException.ThrowIfNull(keepPageIds);

        var keep = keepPageIds.ToArray();

        return await dbContext.Pages
            .Where(page => page.Source == sourceId && !keep.Contains(page.PageId))
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>> GetRevisionsAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceId);

        return await dbContext.Pages
            .AsNoTracking()
            .Where(page => page.Source == sourceId)
            .Select(page => new { page.PageId, page.Revision })
            .ToDictionaryAsync(page => page.PageId, page => page.Revision, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Строит фрагмент вокруг первого совпавшего слова запроса.
    /// Если совпадение не найдено, возвращает начало страницы.
    /// </summary>
    private static string BuildSnippet(string head, string query)
    {
        if (string.IsNullOrEmpty(head))
        {
            return string.Empty;
        }

        var matchIndex = -1;

        foreach (var term in query.Split(
            [' ', '\t', '\n', '\r', ',', '.', '"', '\''],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (term.Length < 3)
            {
                continue;
            }

            matchIndex = head.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (matchIndex >= 0)
            {
                break;
            }
        }

        if (matchIndex < 0)
        {
            return head.Length <= SnippetLength ? head : head[..SnippetLength] + "…";
        }

        var start = Math.Max(0, matchIndex - (SnippetLength / 3));
        var length = Math.Min(SnippetLength, head.Length - start);

        var snippet = head.Substring(start, length);

        if (start > 0)
        {
            snippet = "…" + snippet;
        }

        if (start + length < head.Length)
        {
            snippet += "…";
        }

        return snippet;
    }

    private static IQueryable<WikiPageEntity> ApplyFilters(
        IQueryable<WikiPageEntity> pages,
        WikiSearchQuery query,
        IReadOnlyCollection<string>? allowedNamespaces)
    {
        if (allowedNamespaces is not null)
        {
            pages = pages.Where(page => allowedNamespaces.Contains(page.Namespace));
        }

        if (!string.IsNullOrWhiteSpace(query.Namespace))
        {
            pages = pages.Where(page => page.Namespace == query.Namespace);
        }

        if (query.Tags is { Count: > 0 })
        {
            var tags = query.Tags.ToArray();
            pages = pages.Where(page => tags.All(tag => page.Tags.Contains(tag)));
        }

        if (query.UpdatedAfter is { } updatedAfter)
        {
            pages = pages.Where(page => page.UpdatedAt > updatedAfter);
        }

        return pages;
    }

    private static WikiPage ToDomain(WikiPageEntity entity) => new(
        entity.PageId,
        entity.Path,
        entity.Title,
        entity.ContentMarkdown,
        entity.Namespace,
        entity.Tags,
        entity.UpdatedAt,
        entity.Revision,
        entity.Source);
}
