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
    public Task<SearchResponseDto> SearchAsync(
        string query,
        int? topK,
        WikiSearchFilters? filters,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["query"] = query,
            ["top_k"] = topK,
            ["namespace"] = filters?.Namespace,
            ["tags"] = filters?.Tags is null ? null : string.Join(',', filters.Tags),
            ["updated_after"] = filters?.UpdatedAfter,
        };

        return InvokeAuditedAsync(
            WikiToolNames.Search,
            parameters,
            filters?.Namespace,
            async (call, token) =>
            {
                // FR-62: потолок top_k задаёт сервер, а не клиент.
                var searchQuery = new WikiSearchQuery(
                    Query: query ?? string.Empty,
                    TopK: Math.Clamp(topK ?? options.DefaultTopK, 1, options.MaxTopK),
                    Namespace: filters?.Namespace,
                    Tags: filters?.Tags?.ToArray(),
                    UpdatedAfter: filters?.UpdatedAfter);

                await searchValidator
                    .ValidateAndThrowAsync(searchQuery, token)
                    .ConfigureAwait(false);

                var allowedNamespaces = RequireAllowedNamespaces(call.Subject);

                // FR-63: поиск не должен висеть дольше отведённого времени.
                using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token);
                timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(options.SearchTimeoutMs));

                var results = await index
                    .SearchAsync(searchQuery, allowedNamespaces, timeoutSource.Token)
                    .ConfigureAwait(false);

                return new SearchResponseDto(
                    [.. results.Select(result => new SearchResultDto(
                        result.PageId,
                        result.Path,
                        result.Title,
                        result.Snippet,
                        result.Score,
                        result.UpdatedAt,
                        result.Revision))]);
            },
            cancellationToken);
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
    public Task<PageResponseDto> GetPageAsync(
        string pageIdOrPath,
        int? offset,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["pageIdOrPath"] = pageIdOrPath,
            ["offset"] = offset,
        };

        return InvokeAuditedAsync(
            WikiToolNames.GetPage,
            parameters,
            pageIdOrPath,
            async (call, token) =>
            {
                if (string.IsNullOrWhiteSpace(pageIdOrPath))
                {
                    throw new ValidationException("Не задан идентификатор или путь страницы.");
                }

                var page = await index
                    .GetPageAsync(pageIdOrPath, token)
                    .ConfigureAwait(false)
                    ?? throw new WikiPageNotFoundException($"Страница не найдена: {pageIdOrPath}");

                call.TargetPath = page.Path;

                // FR-21: права проверяются после нахождения страницы, но до выдачи содержимого.
                if (!accessPolicy.CanRead(call.Subject, page.Namespace))
                {
                    throw new WikiAccessDeniedException(
                        $"Нет доступа к разделу wiki: {page.Namespace}");
                }

                return BuildPageResponse(page, offset ?? 0);
            },
            cancellationToken);
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
    public Task<PageListResponseDto> ListPagesAsync(
        string? namespaceFilter,
        int? skip,
        int? take,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["namespace"] = namespaceFilter,
            ["skip"] = skip,
            ["take"] = take,
        };

        return InvokeAuditedAsync(
            WikiToolNames.ListPages,
            parameters,
            namespaceFilter,
            async (call, token) =>
            {
                var effectiveSkip = Math.Max(0, skip ?? 0);
                var effectiveTake = Math.Clamp(
                    take ?? options.MaxListPageSize,
                    1,
                    options.MaxListPageSize);

                var allowedNamespaces = RequireAllowedNamespaces(call.Subject);

                // Запрашиваем на одну запись больше, чтобы узнать о наличии продолжения
                // без отдельного запроса на подсчёт.
                var pages = await index.ListPagesAsync(
                    namespaceFilter,
                    allowedNamespaces,
                    effectiveSkip,
                    effectiveTake + 1,
                    token).ConfigureAwait(false);

                return new PageListResponseDto(
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
                    HasMore: pages.Count > effectiveTake);
            },
            cancellationToken);
    }

    /// <summary>
    /// Возвращает одну секцию страницы wiki по заголовку (раздел 11.2, Could).
    /// </summary>
    /// <param name="pageIdOrPath">Идентификатор или путь страницы.</param>
    /// <param name="sectionTitle">Заголовок искомой секции.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Содержимое найденной секции.</returns>
    /// <exception cref="WikiAccessDeniedException">Доступ к разделу страницы запрещён.</exception>
    /// <exception cref="WikiPageNotFoundException">Страница не найдена.</exception>
    /// <exception cref="WikiSectionNotFoundException">Секция с таким заголовком не найдена на странице.</exception>
    public Task<SectionResponseDto> GetSectionAsync(
        string pageIdOrPath,
        string sectionTitle,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["pageIdOrPath"] = pageIdOrPath,
            ["sectionTitle"] = sectionTitle,
        };

        return InvokeAuditedAsync(
            WikiToolNames.GetSection,
            parameters,
            pageIdOrPath,
            async (call, token) =>
            {
                if (string.IsNullOrWhiteSpace(pageIdOrPath))
                {
                    throw new ValidationException("Не задан идентификатор или путь страницы.");
                }

                if (string.IsNullOrWhiteSpace(sectionTitle))
                {
                    throw new ValidationException("Не задан заголовок секции.");
                }

                var page = await index
                    .GetPageAsync(pageIdOrPath, token)
                    .ConfigureAwait(false)
                    ?? throw new WikiPageNotFoundException($"Страница не найдена: {pageIdOrPath}");

                call.TargetPath = page.Path;

                // FR-21: права проверяются после нахождения страницы, но до выдачи содержимого.
                if (!accessPolicy.CanRead(call.Subject, page.Namespace))
                {
                    throw new WikiAccessDeniedException(
                        $"Нет доступа к разделу wiki: {page.Namespace}");
                }

                var section = MarkdownSections.Find(page.ContentMarkdown, sectionTitle)
                    ?? throw new WikiSectionNotFoundException(
                        $"Секция «{sectionTitle}» не найдена на странице {page.Path}.");

                return new SectionResponseDto(
                    page.PageId,
                    page.Path,
                    section.Title,
                    section.ContentMarkdown,
                    page.Revision);
            },
            cancellationToken);
    }

    /// <summary>
    /// Возвращает последние изменённые страницы, доступные субъекту (раздел 11.2, Could).
    /// </summary>
    /// <param name="take">Сколько записей вернуть; ограничивается сервером.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Последние изменения, отсортированные по убыванию момента изменения.</returns>
    /// <exception cref="WikiAccessDeniedException">Субъекту не доступен ни один раздел.</exception>
    public Task<RecentChangesResponseDto> ListRecentChangesAsync(
        int? take,
        CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["take"] = take,
        };

        return InvokeAuditedAsync(
            WikiToolNames.RecentChanges,
            parameters,
            targetPath: null,
            async (call, token) =>
            {
                var effectiveTake = Math.Clamp(
                    take ?? options.MaxRecentChanges,
                    1,
                    options.MaxRecentChanges);

                var allowedNamespaces = RequireAllowedNamespaces(call.Subject);

                var pages = await index
                    .ListRecentChangesAsync(allowedNamespaces, effectiveTake, token)
                    .ConfigureAwait(false);

                return new RecentChangesResponseDto(
                    [.. pages.Select(page => new RecentChangeDto(
                        page.PageId,
                        page.Path,
                        page.Title,
                        page.Namespace,
                        page.UpdatedAt,
                        page.Revision))]);
            },
            cancellationToken);
    }

    /// <summary>
    /// Выполняет вызов инструмента, гарантируя запись аудита по любому исходу (FR-53).
    /// Ветвление «успех / отказ / не найдено / ошибка» живёт только здесь: иначе каждый
    /// новый инструмент рискует потерять след в аудите на одной из веток.
    /// </summary>
    private async Task<TResponse> InvokeAuditedAsync<TResponse>(
        string tool,
        IReadOnlyDictionary<string, object?> parameters,
        string? targetPath,
        Func<ToolCallContext, CancellationToken, Task<TResponse>> operation,
        CancellationToken cancellationToken)
    {
        var startedTimestamp = Stopwatch.GetTimestamp();
        var call = new ToolCallContext(subjectAccessor.Current, targetPath);

        try
        {
            var response = await operation(call, cancellationToken).ConfigureAwait(false);

            await RecordAsync(AuditOutcome.Allowed, cancellationToken).ConfigureAwait(false);

            return response;
        }
        catch (Exception ex)
        {
            // Аудит пишем даже при отмене запроса, иначе следы обрывов теряются.
            await RecordAsync(Classify(ex), CancellationToken.None).ConfigureAwait(false);

            throw;
        }

        Task RecordAsync(AuditOutcome outcome, CancellationToken token) =>
            auditor.RecordAsync(
                tool,
                call.Subject,
                parameters,
                call.TargetPath,
                outcome,
                startedTimestamp,
                token);
    }

    /// <summary>Сопоставляет исключение с итогом для аудита.</summary>
    private static AuditOutcome Classify(Exception exception) => exception switch
    {
        // UC-4, FR-45: отказ по правам обязан отличаться в аудите от сбоя.
        WikiAccessDeniedException => AuditOutcome.Denied,
        WikiPageNotFoundException or WikiSectionNotFoundException => AuditOutcome.NotFound,
        _ => AuditOutcome.Error,
    };

    /// <summary>
    /// Возвращает доступные субъекту разделы, отказывая, если их нет.
    /// UC-4: отсутствие доступных разделов — отказ, а не пустая выдача без следа в аудите.
    /// </summary>
    private IReadOnlyCollection<string>? RequireAllowedNamespaces(WikiSubject subject)
    {
        var allowedNamespaces = accessPolicy.GetAllowedNamespaces(subject);

        return allowedNamespaces is { Count: 0 }
            ? throw new WikiAccessDeniedException("Субъекту не доступен ни один раздел wiki.")
            : allowedNamespaces;
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
}
