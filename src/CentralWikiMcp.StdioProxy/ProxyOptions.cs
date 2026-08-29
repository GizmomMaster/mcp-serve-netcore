namespace CentralWikiMcp.StdioProxy;

/// <summary>
/// Настройки локального stdio-прокси (UC-5).
/// </summary>
/// <param name="ServerUrl">Адрес центрального MCP-сервера.</param>
/// <param name="ApiKey">API-ключ сервисного аккаунта, если используется (FR-42).</param>
/// <param name="BearerToken">JWT, если harness аутентифицируется токеном (FR-41).</param>
internal sealed record ProxyOptions(string ServerUrl, string? ApiKey, string? BearerToken)
{
    /// <summary>Переменная окружения с адресом сервера.</summary>
    public const string ServerUrlVariable = "WIKI_MCP_SERVER_URL";

    /// <summary>Переменная окружения с API-ключом.</summary>
    public const string ApiKeyVariable = "WIKI_MCP_API_KEY";

    /// <summary>Переменная окружения с JWT.</summary>
    public const string BearerTokenVariable = "WIKI_MCP_TOKEN";

    /// <summary>
    /// Читает настройки из переменных окружения.
    /// Секреты передаются только так — в аргументах командной строки они
    /// были бы видны в списке процессов (NFR-35).
    /// </summary>
    /// <returns>Настройки прокси.</returns>
    /// <exception cref="InvalidOperationException">Адрес сервера не задан.</exception>
    public static ProxyOptions FromEnvironment()
    {
        var serverUrl = Environment.GetEnvironmentVariable(ServerUrlVariable);

        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            throw new InvalidOperationException(
                $"Не задана переменная окружения {ServerUrlVariable} "
                + "с адресом центрального MCP-сервера.");
        }

        return new ProxyOptions(
            serverUrl,
            Environment.GetEnvironmentVariable(ApiKeyVariable),
            Environment.GetEnvironmentVariable(BearerTokenVariable));
    }
}
