using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

namespace Abdt.Infrastructure.OpenTelemetry.Metrics;

/// <summary>
/// Метрики приложения и рантайма (FR-55, NFR-42).
/// </summary>
public static class MetricsExtensions
{
    /// <summary>Имя meter'а для прикладных метрик сервиса.</summary>
    public const string MeterName = "Abdt.WikiMcp";

    /// <summary>
    /// Регистрирует системные метрики и meter приложения.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddOpenTelemetrySystemMetrics(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(new Meter(MeterName));

        services.AddOpenTelemetry().WithMetrics(metrics => metrics
            .AddAspNetCoreInstrumentation()
            .AddRuntimeInstrumentation()
            .AddMeter(MeterName));

        return services;
    }
}
