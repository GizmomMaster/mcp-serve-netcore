using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Индекс wiki в памяти для тестов прикладного слоя.
/// </summary>
internal sealed class FakeWikiIndex : IWikiIndex
{
    private readonly List<WikiPage> pages = [];

    /// <summary>Запросы поиска, дошедшие до индекса.</summary>
    public List<WikiSearchQuery> ReceivedQueries { get; } = [];

    /// <summary>Наборы разрешённых разделов, с которыми вызывался поиск.</summary>
    public List<IReadOnlyCollection<string>?> ReceivedAllowedNamespaces { get; } = [];

    /// <summary>Добавляет страницу в индекс.</summary>
    /// <param name="page">Страница.</param>
    public void Add(WikiPage page) => pages.Add(page);

    /// <inheritdoc />
    public Task<IReadOnlyList<WikiSearchResult>> SearchAsync(
        WikiSearchQuery query,
        IReadOnlyCollection<string>? allowedNamespaces,
        CancellationToken cancellationToken)
    {
        ReceivedQueries.Add(query);
        ReceivedAllowedNamespaces.Add(allowedNamespaces);

        IReadOnlyList<WikiSearchResult> results =
        [
            .. pages
                .Where(page => allowedNamespaces is null
                    || allowedNamespaces.Contains(page.Namespace))
                .Take(query.TopK)
                .Select(page => new WikiSearchResult(
                    page.PageId,
                    page.Path,
                    page.Title,
                    page.ContentMarkdown,
                    1.0,
                    page.UpdatedAt,
                    page.Revision)),
        ];

        return Task.FromResult(results);
    }

    /// <inheritdoc />
    public Task<WikiPage?> GetPageAsync(string pageIdOrPath, CancellationToken cancellationToken) =>
        Task.FromResult(pages.FirstOrDefault(
            page => page.PageId == pageIdOrPath || page.Path == pageIdOrPath));

    /// <inheritdoc />
    public Task<IReadOnlyList<WikiPageSummary>> ListPagesAsync(
        string? namespaceFilter,
        IReadOnlyCollection<string>? allowedNamespaces,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WikiPageSummary> summaries =
        [
            .. pages
                .Where(page => namespaceFilter is null || page.Namespace == namespaceFilter)
                .Where(page => allowedNamespaces is null
                    || allowedNamespaces.Contains(page.Namespace))
                .OrderBy(page => page.Path, StringComparer.Ordinal)
                .Skip(skip)
                .Take(take)
                .Select(page => new WikiPageSummary(
                    page.PageId,
                    page.Path,
                    page.Title,
                    page.Namespace,
                    page.Tags,
                    page.UpdatedAt,
                    page.Revision)),
        ];

        return Task.FromResult(summaries);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<WikiPageSummary>> ListRecentChangesAsync(
        IReadOnlyCollection<string>? allowedNamespaces,
        int take,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<WikiPageSummary> summaries =
        [
            .. pages
                .Where(page => allowedNamespaces is null
                    || allowedNamespaces.Contains(page.Namespace))
                .OrderByDescending(page => page.UpdatedAt)
                .Take(take)
                .Select(page => new WikiPageSummary(
                    page.PageId,
                    page.Path,
                    page.Title,
                    page.Namespace,
                    page.Tags,
                    page.UpdatedAt,
                    page.Revision)),
        ];

        return Task.FromResult(summaries);
    }

    /// <inheritdoc />
    public Task<int> UpsertAsync(
        IReadOnlyCollection<WikiPage> newPages,
        CancellationToken cancellationToken)
    {
        foreach (var page in newPages)
        {
            pages.RemoveAll(existing => existing.PageId == page.PageId);
            pages.Add(page);
        }

        return Task.FromResult(newPages.Count);
    }

    /// <inheritdoc />
    public Task<int> RemoveMissingAsync(
        string sourceId,
        IReadOnlyCollection<string> keepPageIds,
        CancellationToken cancellationToken)
    {
        var removed = pages.RemoveAll(
            page => page.Source == sourceId && !keepPageIds.Contains(page.PageId));

        return Task.FromResult(removed);
    }

    /// <inheritdoc />
    public Task<IReadOnlyDictionary<string, string>> GetRevisionsAsync(
        string sourceId,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, string> revisions = pages
            .Where(page => page.Source == sourceId)
            .ToDictionary(page => page.PageId, page => page.Revision, StringComparer.Ordinal);

        return Task.FromResult(revisions);
    }
}

/// <summary>
/// Приёмник аудита в памяти.
/// </summary>
internal sealed class FakeAuditSink : IAuditSink
{
    /// <summary>Записанные события.</summary>
    public List<AuditEvent> Events { get; } = [];

    /// <inheritdoc />
    public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        Events.Add(auditEvent);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<AuditEvent>> QueryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditEvent> result =
        [
            .. Events
                .Where(auditEvent => auditEvent.OccurredAt >= fromUtc
                    && auditEvent.OccurredAt <= toUtc)
                .OrderByDescending(auditEvent => auditEvent.OccurredAt)
                .Take(take),
        ];

        return Task.FromResult(result);
    }
}

/// <summary>
/// Источник состояния синхронизации в памяти.
/// </summary>
internal sealed class FakeSyncStateStore : ISyncStateStore
{
    private readonly Dictionary<string, SyncState> states = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public Task<SyncState?> GetAsync(string sourceId, CancellationToken cancellationToken) =>
        Task.FromResult(states.GetValueOrDefault(sourceId));

    /// <inheritdoc />
    public Task SaveAsync(SyncState state, CancellationToken cancellationToken)
    {
        states[state.Source] = state;
        return Task.CompletedTask;
    }
}
