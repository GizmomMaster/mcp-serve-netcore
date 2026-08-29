using System.Collections.Concurrent;

namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Счётчик sliding-window в памяти процесса.
/// Используется, когда Redis не настроен (локальная разработка, один инстанс).
/// При горизонтальном масштабировании (NFR-21) лимит становится пер-инстансным,
/// поэтому в проде нужен Redis.
/// </summary>
internal sealed class InMemoryRateLimitCounter(TimeProvider timeProvider) : IRateLimitCounter
{
    private readonly ConcurrentDictionary<string, Window> windows = new();

    /// <inheritdoc />
    public ValueTask<RateLimitResult> TryAcquireAsync(
        string partitionKey,
        RateLimitPolicy policy,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(partitionKey);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        var windowSeconds = policy.WindowSeconds;
        var nowSeconds = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var windowIndex = nowSeconds / windowSeconds;
        var elapsed = nowSeconds % windowSeconds;
        var weight = (double)(windowSeconds - elapsed) / windowSeconds;

        var state = windows.AddOrUpdate(
            partitionKey,
            _ => new Window(windowIndex, 0, 0),
            (_, existing) => Roll(existing, windowIndex));

        int used;
        bool allowed;

        lock (state)
        {
            var estimated = (state.Previous * weight) + state.Current;
            allowed = estimated < policy.PermitLimit;

            if (allowed)
            {
                state.Current++;
                estimated = (state.Previous * weight) + state.Current;
            }

            used = (int)estimated;
        }

        return ValueTask.FromResult(new RateLimitResult(
            allowed,
            policy.PermitLimit,
            used,
            TimeSpan.FromSeconds(windowSeconds - elapsed)));
    }

    private static Window Roll(Window existing, long windowIndex)
    {
        lock (existing)
        {
            var shift = windowIndex - existing.Index;

            switch (shift)
            {
                case 0:
                    return existing;

                case 1:
                    existing.Previous = existing.Current;
                    existing.Current = 0;
                    break;

                default:
                    // Пропущено больше одного окна — прошлая нагрузка уже не влияет.
                    existing.Previous = 0;
                    existing.Current = 0;
                    break;
            }

            existing.Index = windowIndex;
            return existing;
        }
    }

    private sealed class Window(long index, int current, int previous)
    {
        public long Index { get; set; } = index;

        public int Current { get; set; } = current;

        public int Previous { get; set; } = previous;
    }
}
