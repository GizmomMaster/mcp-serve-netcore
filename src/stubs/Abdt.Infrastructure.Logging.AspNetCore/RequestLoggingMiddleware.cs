using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Abdt.Infrastructure.Logging.AspNetCore;

/// <summary>
/// Логирование входящих HTTP-запросов с наполнением сегментного контекста (FR-50, FR-51, NFR-40).
/// </summary>
internal sealed partial class RequestLoggingMiddleware(
    RequestDelegate next,
    ISegmentContextAccessor accessor,
    ILogger<RequestLoggingMiddleware> logger)
{
    private const string BsnOperationIdHeader = "X-Bsn-Operation-Id";
    private const string RequestIdHeader = "X-Request-Id";
    private const string OriginHeader = "X-Origin";

    /// <summary>Обрабатывает запрос.</summary>
    /// <param name="context">HTTP-контекст.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var bsnOperationId = FirstOrNew(context, BsnOperationIdHeader);
        var requestId = FirstOrNew(context, RequestIdHeader);
        var origin = context.Request.Headers[OriginHeader].FirstOrDefault() ?? "unknown";

        accessor.Current = new SegmentContext
        {
            BsnOperationId = bsnOperationId,
            RequestId = requestId,
            Origin = origin,
            Subject = context.User.Identity?.Name,
        };

        context.Response.Headers[BsnOperationIdHeader] = bsnOperationId;
        context.Response.Headers[RequestIdHeader] = requestId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["BsnOperationId"] = bsnOperationId,
            ["RequestId"] = requestId,
            ["Origin"] = origin,
        });

        var timestamp = Stopwatch.GetTimestamp();
        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            var elapsedMs = (long)Stopwatch.GetElapsedTime(timestamp).TotalMilliseconds;
            var path = context.Request.Path.Value ?? "/";

            // Аутентификация отрабатывает ниже по конвейеру, поэтому субъект
            // известен только сейчас — на входе он ещё пуст (FR-51).
            var subject = context.User.Identity?.Name ?? "anonymous";

            if (accessor.Current is { } current)
            {
                current.Subject = context.User.Identity?.Name;
            }

            // Query string не логируем целиком: он может содержать ПД (FR-52).
            LogRequestCompleted(
                logger,
                context.Request.Method,
                path,
                context.Response.StatusCode,
                elapsedMs,
                subject);

            accessor.Current = null;
        }
    }

    private static string FirstOrNew(HttpContext context, string header)
    {
        var value = context.Request.Headers[header].FirstOrDefault();
        return string.IsNullOrWhiteSpace(value) ? Guid.NewGuid().ToString("N") : value;
    }

    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "HTTP {Method} {Path} -> {StatusCode} за {ElapsedMs} мс (subject={Subject})")]
    private static partial void LogRequestCompleted(
        ILogger logger,
        string method,
        string path,
        int statusCode,
        long elapsedMs,
        string subject);
}
