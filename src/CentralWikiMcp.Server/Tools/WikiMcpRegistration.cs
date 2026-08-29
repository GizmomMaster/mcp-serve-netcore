using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Регистрация MCP-сервера и его инструментов (FR-01, FR-02, FR-03).
/// </summary>
public static class WikiMcpRegistration
{
    /// <summary>Имя сервера в MCP-хендшейке.</summary>
    public const string ServerName = "central-wiki-mcp";

    /// <summary>Версия сервиса, публикуемая в MCP-хендшейке.</summary>
    private static readonly string ServerVersion =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Регистрирует MCP-сервер по HTTP-транспорту с инструментами wiki.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddWikiMcpServer(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services
            .AddMcpServer(options => options.ServerInfo = new Implementation
            {
                Name = ServerName,
                Version = ServerVersion,
            })
            .WithHttpTransport()
            .WithTools<WikiTools>();

        return services;
    }
}
