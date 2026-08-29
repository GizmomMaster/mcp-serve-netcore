using Abdt.Infrastructure.Configuration.Validation;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Infrastructure.Health;
using CentralWikiMcp.Infrastructure.Options;
using CentralWikiMcp.Infrastructure.Persistence;
using CentralWikiMcp.Infrastructure.Search;
using CentralWikiMcp.Infrastructure.Security;
using CentralWikiMcp.Infrastructure.Sources;
using CentralWikiMcp.Infrastructure.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure;

/// <summary>
/// Регистрация инфраструктурного слоя.
/// </summary>
public static class InfrastructureRegistration
{
    /// <summary>Имя health-проверки состояния синхронизации.</summary>
    public const string SyncHealthCheckName = "wiki-sync";

    /// <summary>Имя health-проверки базы данных.</summary>
    public const string DatabaseHealthCheckName = "postgres";

    /// <summary>
    /// Регистрирует источник wiki, индекс, аудит и синхронизацию.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddWikiInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        // FR-47: конфигурация проверяется на старте, а не при первом обращении.
        services.AddOptions<WikiDatabaseOptions>()
            .Bind(configuration.GetSection(WikiDatabaseOptions.SectionName))
            .WithAbdtValidation();

        services.AddOptions<WikiSourceOptions>()
            .Bind(configuration.GetSection(WikiSourceOptions.SectionName))
            .WithAbdtValidation();

        services.AddOptions<SyncOptions>()
            .Bind(configuration.GetSection(SyncOptions.SectionName))
            .WithAbdtValidation();

        services.AddOptions<AccessPolicyOptions>()
            .Bind(configuration.GetSection(AccessPolicyOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        var connectionString = configuration
            .GetSection(WikiDatabaseOptions.SectionName)["ConnectionString"];

        services.AddDbContext<WikiDbContext>(dbContextOptions =>
            dbContextOptions.UseNpgsql(connectionString));

        services.AddScoped<IWikiIndex, PostgresWikiIndex>();
        services.AddScoped<IAuditSink, PostgresAuditSink>();
        services.AddScoped<ISyncStateStore, PostgresSyncStateStore>();
        services.AddScoped<WikiSynchronizer>();

        services.AddSingleton<IWikiSource, FileSystemWikiSource>();
        services.AddSingleton<IAccessPolicy, NamespaceAccessPolicy>();

        services.AddHostedService<WikiSyncBackgroundService>();

        return services;
    }

    /// <summary>
    /// Добавляет health-проверки базы и состояния синхронизации (FR-07, FR-08, FR-35).
    /// </summary>
    /// <param name="builder">Билдер health-проверок.</param>
    /// <returns>Тот же билдер для цепочки вызовов.</returns>
    public static IHealthChecksBuilder AddWikiHealthChecks(this IHealthChecksBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.AddCheck<DatabaseHealthCheck>(
            DatabaseHealthCheckName,
            failureStatus: HealthStatus.Unhealthy,
            tags: ["ready", "db"]);

        builder.AddCheck<SyncHealthCheck>(
            SyncHealthCheckName,
            failureStatus: HealthStatus.Degraded,
            tags: ["ready", "sync"]);

        return builder;
    }

    /// <summary>
    /// Применяет миграции БД, если это включено настройкой.
    /// </summary>
    /// <param name="services">Провайдер сервисов.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача применения миграций.</returns>
    public static async Task MigrateWikiDatabaseAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();

        var options = scope.ServiceProvider
            .GetRequiredService<IOptions<WikiDatabaseOptions>>();

        if (!options.Value.MigrateOnStartup)
        {
            return;
        }

        var dbContext = scope.ServiceProvider.GetRequiredService<WikiDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
