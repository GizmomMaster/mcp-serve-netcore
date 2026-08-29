using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CentralWikiMcp.Infrastructure.Persistence;

/// <summary>
/// Фабрика контекста для инструментов EF Core (создание и применение миграций).
/// Нужна, чтобы <c>dotnet ef</c> не поднимал полное приложение с его конфигурацией и секретами.
/// </summary>
internal sealed class WikiDbContextFactory : IDesignTimeDbContextFactory<WikiDbContext>
{
    /// <summary>Строка подключения для design-time, если переменная окружения не задана.</summary>
    private const string DefaultConnectionString =
        "Host=localhost;Port=5433;Database=wikimcp;Username=wikimcp;Password=wikimcp";

    /// <inheritdoc />
    public WikiDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("WIKIMCP_Database__ConnectionString")
            ?? DefaultConnectionString;

        var options = new DbContextOptionsBuilder<WikiDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new WikiDbContext(options);
    }
}
