using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Protoobp.Trace.Bundle;

/// <summary>
/// Корпоративный бандл трассировки (NFR-43). Обязателен для Quality Gates (NFR-10).
/// </summary>
public static class TracingExtensions
{
    /// <summary>Имя ActivitySource сервиса.</summary>
    public const string ActivitySourceName = "Abdt.WikiMcp";

    /// <summary>
    /// Регистрирует трассировку с экспортом по OTLP.
    /// Эндпоинт коллектора задаётся переменной <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="serviceName">Имя сервиса в трассах.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddProtoobpTracing(
        this IServiceCollection services,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
                    .AddSource(ActivitySourceName)
                    .AddAspNetCoreInstrumentation(instrumentation =>
                        // Health-эндпоинты шумят в трассах и не несут смысла.
                        instrumentation.Filter = context =>
                            !context.Request.Path.StartsWithSegments("/health"));

                if (!string.IsNullOrWhiteSpace(
                    Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
                {
                    tracing.AddOtlpExporter();
                }
            });

        return services;
    }
}
