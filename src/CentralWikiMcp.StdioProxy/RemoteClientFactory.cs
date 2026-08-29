using ModelContextProtocol.Client;

namespace CentralWikiMcp.StdioProxy;

/// <summary>
/// Создаёт транспорт к центральному MCP-серверу (UC-5).
/// </summary>
internal static class RemoteClientFactory
{
    /// <summary>
    /// Собирает HTTP-транспорт с заголовками аутентификации.
    /// </summary>
    /// <param name="options">Настройки прокси.</param>
    /// <returns>Транспорт клиента.</returns>
    public static IClientTransport CreateTransport(ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            headers["X-Api-Key"] = options.ApiKey;
        }

        if (!string.IsNullOrWhiteSpace(options.BearerToken))
        {
            headers["Authorization"] = $"Bearer {options.BearerToken}";
        }

        headers["X-Origin"] = "stdio-proxy";

        return new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri(options.ServerUrl),
            Name = "central-wiki-mcp",
            AdditionalHeaders = headers,
            TransportMode = HttpTransportMode.AutoDetect,
        });
    }
}
