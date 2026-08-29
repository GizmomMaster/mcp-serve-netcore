using Microsoft.Extensions.Logging;

namespace CentralWikiMcp.StdioProxy;

/// <summary>
/// Сообщения журнала прокси.
/// </summary>
internal static partial class ProxyLog
{
    /// <summary>
    /// Сообщает, сколько инструментов проброшено с центрального сервера.
    /// </summary>
    /// <param name="logger">Журнал.</param>
    /// <param name="count">Число инструментов.</param>
    /// <param name="serverUrl">Адрес сервера.</param>
    [LoggerMessage(
        EventId = 6000,
        Level = LogLevel.Information,
        Message = "Проброшено инструментов: {Count} с сервера {ServerUrl}")]
    public static partial void ToolsForwarded(ILogger logger, int count, string serverUrl);
}
