using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;

namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Точки подключения ограничения нагрузки (FR-60).
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Регистрирует сервисы ограничения нагрузки.
    /// Если строка подключения к Redis не задана, счётчики держатся в памяти процесса.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.Configure<RateLimitingOptions>(
            configuration.GetSection(RateLimitingOptions.SectionName));

        services.TryAddSingleton(TimeProvider.System);

        var redisConnectionString = configuration
            .GetSection(RateLimitingOptions.SectionName)["RedisConnectionString"];

        if (string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.TryAddSingleton<IRateLimitCounter, InMemoryRateLimitCounter>();
            return services;
        }

        services.TryAddSingleton<IConnectionMultiplexer>(_ =>
        {
            var config = ConfigurationOptions.Parse(redisConnectionString);

            // Fail-open (FR-66): без этого старт приложения падал бы при недоступном Redis.
            config.AbortOnConnectFail = false;
            return ConnectionMultiplexer.Connect(config);
        });

        services.TryAddSingleton<IRateLimitCounter, RedisRateLimitCounter>();
        return services;
    }

    /// <summary>
    /// Включает middleware ограничения нагрузки.
    /// Вызывать после аутентификации, чтобы партиция по ClientId видела субъект.
    /// </summary>
    /// <param name="app">Конвейер приложения.</param>
    /// <returns>Тот же конвейер для цепочки вызовов.</returns>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RateLimitingMiddleware>();
    }
}
