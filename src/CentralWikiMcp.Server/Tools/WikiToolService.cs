using System.Diagnostics;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Server.Contracts;
using CentralWikiMcp.Server.Options;
using CentralWikiMcp.Server.Security;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Прикладная логика MCP-инструментов wiki.
/// Здесь сходятся проверка прав (FR-11, FR-21), серверные лимиты (FR-12, FR-61…FR-63)
/// и аудит (FR-53). Сами инструменты остаются тонкой оболочкой над этим сервисом.
/// </summary>
public sealed class WikiToolService(
    IWikiIndex index,
    IAccessPolicy accessPolicy,
    ISubjectAccessor subjectAccessor,
    ToolAuditor auditor,
    IValidator<WikiSearchQuery> searchValidator,
    IOptions<WikiToolOptions> options)
{
    private readonly WikiToolOptions options = options.Value;

    /// <summary>
    /// Выполняет поиск по wiki (UC-1, раздел 11.3).
    /// </summary>
    /// <param name="query">Поисковый запрос.</param>
    /// <param name="topK">Желаемое число результатов; ограничивается сервером (FR-12).</param>
    /// <param name="filters">Дополнительные фильтры.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результаты поиска, доступные субъекту.</returns>
    /// <exception cref="WikiAccessDeniedException">Субъект не имеет доступа к wiki.</exception>
    /// <exception cref="ValidationException">Параметры запроса некорректны.</exception>
    public async Task<SearchResponseDto> SearchAsync(
        string query,
        int? topK,
        WikiSearchFilters? filters,
        CancellationToken cancellationToken)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var subject = subjectAccessor.Current;

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["query"] = query,
            ["top_k"] = topK,
            ["namespace"] = filters?.Namespace,
            ["tags"] = filters?.Tags is null ? null : string.Join(',', filters.Tags),
            ["updated_after"] = filters?.UpdatedAfter,
        };

        // FR-62: потолок top_k задаёт сервер, а не клиент.
        var effectiveTopK = Math.Clamp(
            topK ?? options.DefaultTopK,
            1,
            options.MaxTopK);

        var searchQuery = new WikiSearchQuery(
            Query: query ?? string.Empty,
            TopK: effectiveTopK,
            Namespace: filters?.Namespace,
            Tags: filters?.Tags?.ToArray(),
            UpdatedAfter: filters?.UpdatedAfter);

        try
        {
            await searchValidator
                .ValidateAndThrowAsync(searchQuery, cancellationToken)
                .ConfigureAwait(false);

            var allowedNamespaces = accessPolicy.GetAllowedNamespaces(subject);

            // UC-4: отсутствие доступных разделов — отказ, а не пустая выдача без следа в аудите.
            if (allowedNamespaces is { Count: 0 })
            {
                await auditor.RecordAsync(
                    WikiToolNames.Search,
                    subject,
                    parameters,
                    filters?.Namespace,
                    AuditOutcome.Denied,
                    startedTimestamp,
                    cancellationToken).ConfigureAwait(false);

                throw new WikiAccessDeniedException("Субъекту не доступен ни один раздел wiki.");
            }

            // FR-63: поиск не должен висеть дольше отведённого времени.
            using var timeoutSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(options.SearchTimeoutMs));

            var results = await index
                .SearchAsync(searchQuery, allowedNamespaces, timeoutSource.Token)
                .ConfigureAwait(false);

            var response = new SearchResponseDto(
                [.. results.Select(result => new SearchResultDto(
                    result.PageId,
                    result.Path,
                    result.Title,
                    result.Snippet,
                    result.Score,
                    result.UpdatedAt,
                    result.Revision))]);

            await auditor.RecordAsync(
                WikiToolNames.Search,
                subject,
                parameters,
                filters?.Namespace,
                AuditOutcome.Allowed,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex) when (ex is not WikiAccessDeniedException)
        {
            await RecordFailureAsync(
                WikiToolNames.Search,
                subject,
                parameters,
                filters?.Namespace,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Возвращает страницу wiki (UC-2, раздел 11.4).
    /// </summary>
    /// <param name="pageIdOrPath">Идентификатор или путь страницы.</param>
    /// <param name="offset">Смещение в символах для частичной выдачи (FR-24).</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница или её фрагмент.</returns>
    /// <exception cref="WikiAccessDeniedException">Доступ к разделу страницы запрещён.</exception>
    /// <exception cref="WikiPageNotFoundException">Страница не найдена.</exception>
    public async Task<PageResponseDto> GetPageAsync(
        string pageIdOrPath,
        int? offset,
        CancellationToken cancellationToken)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var subject = subjectAccessor.Current;

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["pageIdOrPath"] = pageIdOrPath,
            ["offset"] = offset,
        };

        try
        {
            if (string.IsNullOrWhiteSpace(pageIdOrPath))
            {
                throw new ValidationException("Не задан идентификатор или путь страницы.");
            }

            var page = await index
                .GetPageAsync(pageIdOrPath, cancellationToken)
                .ConfigureAwait(false);

            if (page is null)
            {
                await auditor.RecordAsync(
                    WikiToolNames.GetPage,
                    subject,
                    parameters,
                    pageIdOrPath,
                    AuditOutcome.NotFound,
                    startedTimestamp,
                    cancellationToken).ConfigureAwait(false);

                throw new WikiPageNotFoundException($"Страница не найдена: {pageIdOrPath}");
            }

            // FR-21: права проверяются после нахождения страницы, но до выдачи содержимого.
            if (!accessPolicy.CanRead(subject, page.Namespace))
            {
                await auditor.RecordAsync(
                    WikiToolNames.GetPage,
                    subject,
                    parameters,
                    page.Path,
                    AuditOutcome.Denied,
                    startedTimestamp,
                    cancellationToken).ConfigureAwait(false);

                throw new WikiAccessDeniedException(
                    $"Нет доступа к разделу wiki: {page.Namespace}");
            }

            var response = BuildPageResponse(page, offset ?? 0);

            await auditor.RecordAsync(
                WikiToolNames.GetPage,
                subject,
                parameters,
                page.Path,
                AuditOutcome.Allowed,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
            when (ex is not WikiAccessDeniedException and not WikiPageNotFoundException)
        {
            await RecordFailureAsync(
                WikiToolNames.GetPage,
                subject,
                parameters,
                pageIdOrPath,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Возвращает список страниц, доступных субъекту.
    /// </summary>
    /// <param name="namespaceFilter">Фильтр по разделу wiki.</param>
    /// <param name="skip">Сколько записей пропустить.</param>
    /// <param name="take">Сколько записей вернуть; ограничивается сервером.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Порция метаданных страниц.</returns>
    /// <exception cref="WikiAccessDeniedException">Субъекту не доступен ни один раздел.</exception>
    public async Task<PageListResponseDto> ListPagesAsync(
        string? namespaceFilter,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var subject = subjectAccessor.Current;

        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["namespace"] = namespaceFilter,
            ["skip"] = skip,
            ["take"] = take,
        };

        var effectiveSkip = Math.Max(0, skip ?? 0);
        var effectiveTake = Math.Clamp(take ?? options.MaxListPageSize, 1, options.MaxListPageSize);

        try
        {
            var allowedNamespaces = accessPolicy.GetAllowedNamespaces(subject);

            if (allowedNamespaces is { Count: 0 })
            {
                await auditor.RecordAsync(
                    WikiToolNames.ListPages,
                    subject,
                    parameters,
                    namespaceFilter,
                    AuditOutcome.Denied,
                    startedTimestamp,
                    cancellationToken).ConfigureAwait(false);

                throw new WikiAccessDeniedException("Субъекту не доступен ни один раздел wiki.");
            }

            // Запрашиваем на одну запись больше, чтобы узнать о наличии продолжения
            // без отдельного запроса на подсчёт.
            var pages = await index.ListPagesAsync(
                namespaceFilter,
                allowedNamespaces,
                effectiveSkip,
                effectiveTake + 1,
                cancellationToken).ConfigureAwait(false);

            var hasMore = pages.Count > effectiveTake;

            var response = new PageListResponseDto(
                [.. pages.Take(effectiveTake).Select(page => new PageSummaryDto(
                    page.PageId,
                    page.Path,
                    page.Title,
                    page.Namespace,
                    page.Tags,
                    page.UpdatedAt,
                    page.Revision))],
                effectiveSkip,
                effectiveTake,
                hasMore);

            await auditor.RecordAsync(
                WikiToolNames.ListPages,
                subject,
                parameters,
                namespaceFilter,
                AuditOutcome.Allowed,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex) when (ex is not WikiAccessDeniedException)
        {
            await RecordFailureAsync(
                WikiToolNames.ListPages,
                subject,
                parameters,
                namespaceFilter,
                startedTimestamp,
                cancellationToken).ConfigureAwait(false);

            throw;
        }
    }

    /// <summary>
    /// Нарезает содержимое страницы под лимит ответа (FR-24, FR-61).
    /// </summary>
    private PageResponseDto BuildPageResponse(WikiPage page, int offset)
    {
        var content = page.ContentMarkdown;
        var totalChars = content.Length;

        var start = Math.Clamp(offset, 0, totalChars);
        var chunkSize = Math.Min(options.PageChunkChars, options.MaxResponseChars);
        var length = Math.Min(chunkSize, totalChars - start);

        var slice = length <= 0 ? string.Empty : content.Substring(start, length);
        var end = start + length;
        var truncated = end < totalChars;

        return new PageResponseDto(
            page.PageId,
            page.Path,
            page.Title,
            slice,
            page.UpdatedAt,
            page.Revision,
            page.Source,
            truncated,
            start,
            totalChars,
            truncated ? end : null);
    }

    private async Task RecordFailureAsync(
        string tool,
        WikiSubject subject,
        IReadOnlyDictionary<string, object?> parameters,
        string? targetPath,
        long startedTimestamp,
        CancellationToken cancellationToken)
    {
        // Аудит пишем даже при отмене запроса, иначе следы обрывов теряются.
        await auditor.RecordAsync(
            tool,
            subject,
            parameters,
            targetPath,
            AuditOutcome.Error,
            startedTimestamp,
            CancellationToken.None).ConfigureAwait(false);

        cancellationToken.ThrowIfCancellationRequested();
    }
}
