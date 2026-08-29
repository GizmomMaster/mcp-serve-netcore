using System.Text.Json.Serialization;
using Abdt.Infrastructure.Configuration.Validation;
using Abdt.Infrastructure.Configuration.Vault;
using Abdt.Infrastructure.Logging.AspNetCore;
using Abdt.Infrastructure.Monitoring.HealthCheck;
using Abdt.Infrastructure.OpenTelemetry.Metrics;
using Abdt.Infrastructure.RateLimiting;
using Asp.Versioning;
using CentralWikiMcp.Infrastructure;
using CentralWikiMcp.Server.Endpoints;
using CentralWikiMcp.Server.Options;
using CentralWikiMcp.Server.Security;
using CentralWikiMcp.Server.Tools;
using FluentValidation;
using Protoobp.Trace.Bundle;

var builder = WebApplication.CreateBuilder(args);

// NFR-35, FR-46: секреты приходят из Vault или переменных окружения, не из appsettings.json.
builder.Configuration.AddVault();

// NFR-40: корпоративное логирование с маскированием ПД по умолчанию.
builder.Host.AddAbdtLogger();

builder.Services.AddRequestLogging();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);

// FR-04, NFR-32: валидация входных параметров.
builder.Services.AddOptions<WikiToolOptions>()
    .Bind(builder.Configuration.GetSection(WikiToolOptions.SectionName))
    .WithAbdtValidation();

builder.Services.AddValidatorsFromAssemblyContaining<WikiSearchQueryValidator>();

// Источник wiki, индекс, аудит, синхронизация.
builder.Services.AddWikiInfrastructure(builder.Configuration);

builder.Services.AddScoped<ISubjectAccessor, HttpSubjectAccessor>();
builder.Services.AddScoped<ToolAuditor>();
builder.Services.AddScoped<WikiToolService>();

// Перечисления в ответах API отдаём строками: числовой outcome в выгрузке
// аудита (FR-54) нечитаем для внешних систем и людей.
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// FR-09: версионирование API.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});

// FR-40…FR-45: аутентификация обязательна, поддержаны JWT и API-key.
builder.Services.AddWikiAuthentication(builder.Configuration, builder.Environment);
builder.Services.AddWikiAuthorization(builder.Configuration);

// FR-60: двухуровневое ограничение нагрузки.
builder.Services.AddRateLimiting(builder.Configuration);

// FR-07, FR-08: health-эндпоинты и проверки зависимостей.
builder.Services.AddAutoHealthChecks().AddWikiHealthChecks();

// NFR-42, NFR-43: метрики и трассировка.
builder.Services.AddOpenTelemetrySystemMetrics();
builder.Services.AddProtoobpTracing(WikiMcpRegistration.ServerName);

// FR-01, FR-02, FR-03: MCP-сервер по HTTP-транспорту с инструментами wiki.
builder.Services.AddWikiMcpServer();

var app = builder.Build();

// FR-05, FR-06: единый формат ошибок и корректная обработка сбоев.
app.UseExceptionHandler(ErrorHandling.Handler);

app.UseRequestLogging();

app.UseAuthentication();

// После аутентификации: партиция лимита по ClientId должна видеть субъект (FR-64).
app.UseRateLimiting();

app.UseAuthorization();

app.UseAutoHealthChecks();

// FR-02: MCP-эндпоинт, к которому подключается harness.
// FR-65: бизнес-лимит поверх общего DDoS-лимита для основного рабочего эндпоинта.
app.MapMcp("/mcp")
    .RequireAuthorization()
    .WithMetadata(new RateLimitAttribute("search"));

app.MapAdminEndpoints();

// FR-31: миграции применяются до приёма трафика.
await app.Services.MigrateWikiDatabaseAsync(CancellationToken.None);

await app.RunAsync();

/// <summary>
/// Точка входа, вынесенная для интеграционных тестов.
/// </summary>
public partial class Program;
