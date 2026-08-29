using System.ComponentModel;
using CentralWikiMcp.Server.Contracts;
using FluentValidation;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// MCP-инструменты доступа к wiki (раздел 11.2).
/// Описания параметров попадают в схему инструментов, которую видит LLM (FR-03),
/// поэтому формулировки здесь — часть контракта.
/// </summary>
[McpServerToolType]
public sealed class WikiTools(WikiToolService service)
{
    /// <summary>
    /// Ищет страницы в корпоративной wiki (UC-1, раздел 11.3).
    /// </summary>
    /// <param name="query">Поисковый запрос на естественном языке или по ключевым словам.</param>
    /// <param name="topK">Сколько результатов вернуть. По умолчанию 5, максимум ограничен сервером.</param>
    /// <param name="filters">Необязательные фильтры по разделу, тегам и дате обновления.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Список найденных страниц с фрагментами и метаданными.</returns>
    [McpServerTool(Name = WikiToolNames.Search)]
    [Description(
        "Ищет страницы в корпоративной wiki по запросу. "
        + "Возвращает релевантные фрагменты с путём, заголовком, оценкой релевантности и ревизией. "
        + "Выдача ограничена разделами, доступными вызывающему.")]
    public Task<SearchResponseDto> SearchAsync(
        [Description("Поисковый запрос.")] string query,
        [Description("Сколько результатов вернуть (по умолчанию 5).")] int? topK = null,
        [Description("Фильтры: раздел wiki, теги, дата обновления.")] WikiSearchFilters? filters = null,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(
            () => service.SearchAsync(query, topK, filters, cancellationToken));

    /// <summary>
    /// Возвращает содержимое страницы wiki (UC-2, раздел 11.4).
    /// </summary>
    /// <param name="pageIdOrPath">Идентификатор страницы либо её путь вида <c>runbooks/deploy</c>.</param>
    /// <param name="offset">Смещение в символах для чтения крупной страницы по частям.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Страница целиком либо её фрагмент.</returns>
    [McpServerTool(Name = WikiToolNames.GetPage)]
    [Description(
        "Возвращает содержимое страницы wiki в Markdown по её идентификатору или пути. "
        + "Крупные страницы отдаются частями: если поле truncated равно true, "
        + "повторите вызов со значением offset, равным nextOffset.")]
    public Task<PageResponseDto> GetPageAsync(
        [Description("Идентификатор страницы или путь, например runbooks/deploy.")]
        string pageIdOrPath,
        [Description("Смещение в символах для частичного чтения.")] int? offset = null,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(
            () => service.GetPageAsync(pageIdOrPath, offset, cancellationToken));

    /// <summary>
    /// Перечисляет доступные страницы wiki.
    /// </summary>
    /// <param name="wikiNamespace">Фильтр по разделу wiki.</param>
    /// <param name="skip">Сколько записей пропустить.</param>
    /// <param name="take">Сколько записей вернуть.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Порция метаданных страниц.</returns>
    [McpServerTool(Name = WikiToolNames.ListPages)]
    [Description(
        "Перечисляет страницы wiki, доступные вызывающему, с их путями, разделами и тегами. "
        + "Используйте для навигации по структуре wiki, когда точный путь страницы неизвестен.")]
    public Task<PageListResponseDto> ListPagesAsync(
        [Description("Раздел wiki для фильтрации.")] string? wikiNamespace = null,
        [Description("Сколько записей пропустить.")] int? skip = null,
        [Description("Сколько записей вернуть.")] int? take = null,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(
            () => service.ListPagesAsync(wikiNamespace, skip, take, cancellationToken));

    /// <summary>
    /// Возвращает одну секцию страницы wiki по заголовку (раздел 11.2, Could).
    /// </summary>
    /// <param name="pageIdOrPath">Идентификатор страницы либо её путь вида <c>runbooks/deploy</c>.</param>
    /// <param name="sectionTitle">Заголовок секции, например «Откат».</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Заголовок и содержимое найденной секции.</returns>
    [McpServerTool(Name = WikiToolNames.GetSection)]
    [Description(
        "Возвращает содержимое одной секции страницы wiki по заголовку (например, «Откат»). "
        + "Используйте, когда нужна конкретная часть большой страницы, а не всё её содержимое.")]
    public Task<SectionResponseDto> GetSectionAsync(
        [Description("Идентификатор страницы или путь, например runbooks/deploy.")]
        string pageIdOrPath,
        [Description("Заголовок секции, например «Откат».")] string sectionTitle,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(
            () => service.GetSectionAsync(pageIdOrPath, sectionTitle, cancellationToken));

    /// <summary>
    /// Возвращает последние изменённые страницы wiki (раздел 11.2, Could).
    /// </summary>
    /// <param name="take">Сколько записей вернуть. Ограничивается сервером.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Изменения, доступные вызывающему, отсортированные по убыванию времени.</returns>
    [McpServerTool(Name = WikiToolNames.RecentChanges)]
    [Description(
        "Возвращает страницы wiki, доступные вызывающему, изменённые последними. "
        + "Используйте, чтобы узнать, что недавно поменялось в wiki.")]
    public Task<RecentChangesResponseDto> RecentChangesAsync(
        [Description("Сколько записей вернуть (по умолчанию и максимум задаёт сервер).")]
        int? take = null,
        CancellationToken cancellationToken = default) =>
        TranslateErrorsAsync(
            () => service.ListRecentChangesAsync(take, cancellationToken));

    /// <summary>
    /// Переводит доменные ошибки в <see cref="McpException"/>: её текст доходит
    /// до клиента, тогда как прочие исключения SDK скрывает за общей формулировкой.
    /// Модели нужна причина отказа, иначе она будет повторять безнадёжный вызов.
    /// </summary>
    /// <typeparam name="TResult">Тип результата инструмента.</typeparam>
    /// <param name="action">Вызов прикладного сервиса.</param>
    /// <returns>Результат вызова.</returns>
    private static async Task<TResult> TranslateErrorsAsync<TResult>(
        Func<Task<TResult>> action)
    {
        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (ValidationException ex)
        {
            throw new McpException($"Некорректные параметры вызова: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is WikiAccessDeniedException
            or WikiPageNotFoundException
            or WikiSectionNotFoundException)
        {
            throw new McpException(ex.Message, ex);
        }
    }
}
