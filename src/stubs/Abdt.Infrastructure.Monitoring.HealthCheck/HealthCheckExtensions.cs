using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Abdt.Infrastructure.Monitoring.HealthCheck;

/// <summary>
/// Корпоративные health-эндпоинты (FR-07, FR-08, NFR-22).
/// </summary>
public static class HealthCheckExtensions
{
    /// <summary>Liveness: сервис жив.</summary>
    public const string LivenessPath = "/health";

    /// <summary>Readiness: сервис готов принимать нагрузку, со сводкой по зависимостям.</summary>
    public const string FullPath = "/health/full";

    /// <summary>Формат для Zabbix: одно число (1 — здоров, 0 — нет).</summary>
    public const string ZabbixPath = "/health/zabbix";

    /// <summary>Тег проверок, входящих в readiness.</summary>
    public const string ReadinessTag = "ready";

    /// <summary>
    /// Регистрирует инфраструктуру health-проверок.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <returns>Билдер для добавления конкретных проверок.</returns>
    public static IHealthChecksBuilder AddAutoHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        return services.AddHealthChecks();
    }

    /// <summary>
    /// Публикует эндпоинты <c>/health</c>, <c>/health/full</c> и <c>/health/zabbix</c>.
    /// </summary>
    /// <param name="app">Конвейер приложения.</param>
    /// <returns>Тот же конвейер для цепочки вызовов.</returns>
    public static IEndpointRouteBuilder UseAutoHealthChecks(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Liveness: без обращения к зависимостям, иначе перезапуски по чужой недоступности.
        app.MapHealthChecks(LivenessPath, new HealthCheckOptions
        {
            Predicate = _ => false,
            AllowCachingResponses = false,
        }).AllowAnonymous();

        // Readiness (FR-08): полная картина с зависимостями.
        app.MapHealthChecks(FullPath, new HealthCheckOptions
        {
            ResponseWriter = WriteFullReportAsync,
            AllowCachingResponses = false,
        }).AllowAnonymous();

        // Zabbix: одно число, чтобы не парсить JSON в шаблоне мониторинга.
        app.MapHealthChecks(ZabbixPath, new HealthCheckOptions
        {
            ResponseWriter = WriteZabbixAsync,
            AllowCachingResponses = false,
        }).AllowAnonymous();

        return app;
    }

    private static Task WriteZabbixAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        return context.Response.WriteAsync(report.Status == HealthStatus.Healthy ? "1" : "0");
    }

    private static async Task WriteFullReportAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds
                .ToString("F1", CultureInfo.InvariantCulture),
            entries = report.Entries.ToDictionary(
                entry => entry.Key,
                entry => new
                {
                    status = entry.Value.Status.ToString(),
                    durationMs = entry.Value.Duration.TotalMilliseconds
                        .ToString("F1", CultureInfo.InvariantCulture),
                    // Описание упавшей проверки ASP.NET Core заполняет текстом исключения,
                    // а он может содержать строку подключения или учётные данные. Эндпоинт
                    // анонимный, поэтому наружу идёт только факт сбоя (NFR-35).
                    description = entry.Value.Exception is null ? entry.Value.Description : null,
                    error = entry.Value.Exception is null ? null : "check failed",
                    data = entry.Value.Data.Count == 0 ? null : entry.Value.Data,
                }),
        };

        await context.Response
            .WriteAsync(JsonSerializer.Serialize(payload, JsonOptions))
            .ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };
}
