using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace Abdt.Infrastructure.Logging.AspNetCore;

/// <summary>
/// Точки подключения корпоративного логирования (FR-50, NFR-40).
/// </summary>
public static class LoggingExtensions
{
    /// <summary>
    /// Регистрирует структурированный логгер с маскированием ПД по умолчанию.
    /// </summary>
    /// <param name="builder">Билдер хоста.</param>
    /// <returns>Тот же билдер для цепочки вызовов.</returns>
    public static IHostBuilder AddAbdtLogger(this IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseSerilog((context, services, configuration) => configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.With<MaskingEnricher>()
            .WriteTo.Console(
                formatProvider: CultureInfo.InvariantCulture,
                outputTemplate:
                "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} {Level:u3}] "
                + "{BsnOperationId} {RequestId} {Message:lj}{NewLine}{Exception}"));

        return builder;
    }

    /// <summary>
    /// Регистрирует сервисы логирования запросов.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddRequestLogging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<ISegmentContextAccessor, SegmentContextAccessor>();
        return services;
    }

    /// <summary>
    /// Включает middleware логирования запросов.
    /// </summary>
    /// <param name="app">Конвейер приложения.</param>
    /// <returns>Тот же конвейер для цепочки вызовов.</returns>
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        return app.UseMiddleware<RequestLoggingMiddleware>();
    }
}
