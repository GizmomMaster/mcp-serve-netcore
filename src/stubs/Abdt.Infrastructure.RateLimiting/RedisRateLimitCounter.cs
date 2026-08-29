using System.Globalization;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Счётчик на Redis: two-bucket sliding window через Lua-скрипт (FR-60, FR-64).
/// Сам сервис остаётся stateless (NFR-20) — состояние только в Redis.
/// </summary>
internal sealed partial class RedisRateLimitCounter(
    IConnectionMultiplexer redis,
    TimeProvider timeProvider,
    ILogger<RedisRateLimitCounter> logger) : IRateLimitCounter
{
    /// <summary>
    /// Атомарно оценивает нагрузку по текущему и предыдущему окну и инкрементирует счётчик.
    /// Предыдущее окно учитывается с весом, пропорциональным неистёкшей части.
    /// </summary>
    private const string Script = """
        local cur_key = KEYS[1]
        local prev_key = KEYS[2]
        local limit = tonumber(ARGV[1])
        local window = tonumber(ARGV[2])
        local elapsed = tonumber(ARGV[3])

        local cur = tonumber(redis.call('GET', cur_key) or '0')
        local prev = tonumber(redis.call('GET', prev_key) or '0')

        local weight = (window - elapsed) / window
        local estimated = prev * weight + cur

        if estimated >= limit then
          return {0, math.floor(estimated)}
        end

        cur = redis.call('INCR', cur_key)
        if cur == 1 then
          redis.call('EXPIRE', cur_key, window * 2)
        end

        return {1, math.floor(prev * weight + cur)}
        """;

    /// <inheritdoc />
    public async ValueTask<RateLimitResult> TryAcquireAsync(
        string partitionKey,
        RateLimitPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var window = policy.WindowSeconds;
        var nowSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var windowIndex = nowSeconds / window;
        var elapsed = nowSeconds % window;

        try
        {
            var database = redis.GetDatabase();
            var result = (RedisValue[]?)await database.ScriptEvaluateAsync(
                Script,
                [
                    BuildKey(partitionKey, windowIndex),
                    BuildKey(partitionKey, windowIndex - 1),
                ],
                [
                    policy.PermitLimit,
                    window,
                    elapsed,
                ]).ConfigureAwait(false);

            if (result is not { Length: 2 })
            {
                return Allow(policy);
            }

            var allowed = (int)result[0] == 1;
            var used = (int)result[1];

            return new RateLimitResult(
                allowed,
                policy.PermitLimit,
                used,
                TimeSpan.FromSeconds(window - elapsed));
        }
        catch (RedisException ex)
        {
            // FR-66: недоступность Redis не должна ронять трафик.
            LogFailOpen(logger, ex.Message);
            return Allow(policy);
        }
        catch (TimeoutException ex)
        {
            LogFailOpen(logger, ex.Message);
            return Allow(policy);
        }
    }

    private static RateLimitResult Allow(RateLimitPolicy policy) =>
        new(true, policy.PermitLimit, 0, TimeSpan.Zero);

    private static RedisKey BuildKey(string partitionKey, long windowIndex) =>
        (RedisKey)string.Create(
            CultureInfo.InvariantCulture,
            $"rl:{partitionKey}:{windowIndex}");

    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Warning,
        Message = "Rate limiting: Redis недоступен, запрос пропущен (fail-open). Причина: {Reason}")]
    private static partial void LogFailOpen(ILogger logger, string reason);
}
