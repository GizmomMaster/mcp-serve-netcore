using CentralWikiMcp.StdioProxy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Server;

// UC-5: harness умеет запускать только локальный stdio-процесс.
// Этот прокси принимает stdio-запросы и переадресует их центральному
// MCP-серверу по HTTP. Wiki при этом на машине harness не хранится (BR-02).
var options = ProxyOptions.FromEnvironment();

// stdout занят транспортом MCP — логи уходят только в stderr.
using var loggerFactory = LoggerFactory.Create(logging => logging
    .AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace)
    .SetMinimumLevel(LogLevel.Information));

var proxyLogger = loggerFactory.CreateLogger("Proxy");

// Список инструментов забираем у центрального сервера до сборки хоста:
// после Build() состав инструментов уже зафиксирован в capabilities.
await using var remoteClient = await McpClient
    .CreateAsync(RemoteClientFactory.CreateTransport(options), loggerFactory: loggerFactory)
    .ConfigureAwait(false);

var remoteTools = await remoteClient.ListToolsAsync().ConfigureAwait(false);

ProxyLog.ToolsForwarded(proxyLogger, remoteTools.Count, options.ServerUrl);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(console => console.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(options);

builder.Services
    .AddMcpServer(serverOptions =>
    {
        serverOptions.ServerInfo = new ModelContextProtocol.Protocol.Implementation
        {
            Name = "central-wiki-mcp-proxy",
            Version = "1.0.0",
        };
    })
    .WithStdioServerTransport()

    // McpClientTool — это AIFunction, поэтому инструмент удалённого сервера
    // публикуется локально как есть: вызов уходит обратно тому же клиенту.
    .WithTools(remoteTools.Select(tool => McpServerTool.Create(tool)));

var host = builder.Build();

await host.RunAsync().ConfigureAwait(false);
