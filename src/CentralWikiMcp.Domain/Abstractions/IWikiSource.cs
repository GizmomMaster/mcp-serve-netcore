using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Domain.Abstractions;

/// <summary>
/// Источник страниц wiki (FR-30). Реализации: файловая папка, Git, Confluence.
/// Смена источника не должна затрагивать MCP-клиентов (раздел 4, «Гибкость»).
/// </summary>
public interface IWikiSource
{
    /// <summary>Идентификатор источника, попадает в <see cref="WikiPage.Source"/>.</summary>
    string SourceId { get; }

    /// <summary>
    /// Перечисляет страницы источника потоково, чтобы не держать всю wiki в памяти.
    /// </summary>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Асинхронная последовательность страниц.</returns>
    IAsyncEnumerable<WikiPage> EnumeratePagesAsync(CancellationToken cancellationToken);
}
