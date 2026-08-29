using Abdt.Infrastructure.RateLimiting;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Infrastructure.Sync;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace CentralWikiMcp.Server.Endpoints;

/// <summary>
/// Административные и аудиторские эндпоинты (разделы 8.3, 8.4).
/// </summary>
public static class AdminEndpoints
{
    /// <summary>Политика авторизации администратора MCP-сервера.</summary>
    public const string AdminPolicy = "wiki-admin";

    /// <summary>Политика авторизации аудитора.</summary>
    public const string AuditorPolicy = "wiki-auditor";

    /// <summary>
    /// Публикует административные эндпоинты.
    /// </summary>
    /// <param name="app">Маршрутизатор.</param>
    /// <returns>Тот же маршрутизатор для цепочки вызовов.</returns>
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var admin = app.MapGroup("/api/v1/admin")
            .RequireAuthorization(AdminPolicy)
            .WithTags("Admin");

        // FR-33: ручной запуск синхронизации администратором.
        admin.MapPost("/sync", RunSyncAsync)
            .WithName("RunSync")
            .WithSummary("Запускает синхронизацию индекса wiki.")
            .WithMetadata(new RateLimitAttribute("admin"));

        // FR-34: состояние последней синхронизации.
        admin.MapGet("/sync/status", GetSyncStatusAsync)
            .WithName("GetSyncStatus")
            .WithSummary("Возвращает состояние последней синхронизации.");

        // FR-54: выгрузка аудита во внешнюю систему.
        app.MapGet("/api/v1/audit", QueryAuditAsync)
            .RequireAuthorization(AuditorPolicy)
            .WithTags("Audit")
            .WithName("QueryAudit")
            .WithSummary("Возвращает события аудита за период.")
            .WithMetadata(new RateLimitAttribute("audit"));

        return app;
    }

    /// <summary>
    /// Запускает синхронизацию индекса (FR-33, FR-37).
    /// </summary>
    /// <param name="synchronizer">Синхронизатор.</param>
    /// <param name="full">Выполнять ли полное переиндексирование.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Итоговое состояние синхронизации.</returns>
    private static async Task<Ok<SyncStatusResponse>> RunSyncAsync(
        WikiSynchronizer synchronizer,
        bool? full,
        CancellationToken cancellationToken)
    {
        var state = await synchronizer
            .SynchronizeAsync(full ?? false, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(SyncStatusResponse.From(state));
    }

    /// <summary>
    /// Возвращает состояние последней синхронизации (FR-34).
    /// </summary>
    /// <param name="stateStore">Хранилище состояния.</param>
    /// <param name="source">Источник wiki.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Состояние или 404, если синхронизация не запускалась.</returns>
    private static async Task<Results<Ok<SyncStatusResponse>, NotFound>> GetSyncStatusAsync(
        ISyncStateStore stateStore,
        IWikiSource source,
        CancellationToken cancellationToken)
    {
        var state = await stateStore
            .GetAsync(source.SourceId, cancellationToken)
            .ConfigureAwait(false);

        return state is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(SyncStatusResponse.From(state));
    }

    /// <summary>
    /// Возвращает события аудита за период (FR-54).
    /// </summary>
    /// <param name="auditSink">Хранилище аудита.</param>
    /// <param name="from">Начало периода; по умолчанию сутки назад.</param>
    /// <param name="to">Конец периода; по умолчанию текущий момент.</param>
    /// <param name="take">Максимум записей.</param>
    /// <param name="timeProvider">Источник времени.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>События аудита.</returns>
    private static async Task<Ok<AuditQueryResponse>> QueryAuditAsync(
        IAuditSink auditSink,
        DateTimeOffset? from,
        DateTimeOffset? to,
        int? take,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var fromUtc = from ?? now.AddDays(-1);
        var toUtc = to ?? now;
        var limit = Math.Clamp(take ?? 100, 1, 1000);

        var events = await auditSink
            .QueryAsync(fromUtc, toUtc, limit, cancellationToken)
            .ConfigureAwait(false);

        return TypedResults.Ok(new AuditQueryResponse(fromUtc, toUtc, events.Count, events));
    }
}
