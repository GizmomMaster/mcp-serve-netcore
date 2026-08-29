using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Двухуровневое ограничение нагрузки (FR-60, FR-64, FR-65):
/// сначала общий DDoS-лимит по партиции, затем бизнес-лимит эндпоинта.
/// </summary>
internal sealed partial class RateLimitingMiddleware(
    RequestDelegate next,
    IRateLimitCounter counter,
    IOptions<RateLimitingOptions> options,
    ILogger<RateLimitingMiddleware> logger)
{
    private readonly RateLimitingOptions options = options.Value;

    /// <summary>Обрабатывает запрос.</summary>
    /// <param name="context">HTTP-контекст.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!options.Enabled)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var partitionKey = ResolvePartitionKey(context);

        // Уровень 1 — общая защита от DDoS.
        var general = await counter
            .TryAcquireAsync($"general:{partitionKey}", options.General, context.RequestAborted)
            .ConfigureAwait(false);

        if (!general.Allowed)
        {
            await RejectAsync(context, general, "general").ConfigureAwait(false);
            return;
        }

        // Уровень 2 — бизнес-лимит конкретного эндпоинта.
        var policyName = context.GetEndpoint()?.Metadata.GetMetadata<RateLimitAttribute>()?.Policy;

        if (policyName is not null
            && options.Policies.TryGetValue(policyName, out var businessPolicy))
        {
            var business = await counter
                .TryAcquireAsync($"{policyName}:{partitionKey}", businessPolicy, context.RequestAborted)
                .ConfigureAwait(false);

            if (!business.Allowed)
            {
                await RejectAsync(context, business, policyName).ConfigureAwait(false);
                return;
            }

            WriteHeaders(context, business);
        }
        else
        {
            WriteHeaders(context, general);
        }

        await next(context).ConfigureAwait(false);
    }

    private string ResolvePartitionKey(HttpContext context)
    {
        if (options.Partition == RateLimitPartition.ClientId)
        {
            var subject = context.User.Identity?.Name;
            if (!string.IsNullOrWhiteSpace(subject))
            {
                return subject;
            }
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static void WriteHeaders(HttpContext context, RateLimitResult result)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Headers["X-RateLimit-Limit"] =
            result.Limit.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers["X-RateLimit-Remaining"] =
            result.Remaining.ToString(CultureInfo.InvariantCulture);
    }

    private async Task RejectAsync(HttpContext context, RateLimitResult result, string scope)
    {
        LogRejected(logger, scope, context.Request.Path.Value ?? "/");

        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter =
            ((int)Math.Ceiling(result.RetryAfter.TotalSeconds))
            .ToString(CultureInfo.InvariantCulture);

        WriteHeaders(context, result);

        await context.Response.WriteAsJsonAsync(new
        {
            error = "rate_limit_exceeded",
            scope,
            retryAfterSeconds = (int)Math.Ceiling(result.RetryAfter.TotalSeconds),
        }).ConfigureAwait(false);
    }

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Rate limit превышен: политика={Scope}, путь={Path}")]
    private static partial void LogRejected(ILogger logger, string scope, string path);
}
