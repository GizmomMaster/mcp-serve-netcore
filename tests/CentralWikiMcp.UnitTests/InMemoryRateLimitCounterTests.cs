using Xunit;
using Abdt.Infrastructure.RateLimiting;
using Microsoft.Extensions.Time.Testing;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки скользящего окна ограничения нагрузки (FR-60, FR-64).
/// </summary>
public sealed class InMemoryRateLimitCounterTests
{
    private static readonly RateLimitPolicy Policy = new()
    {
        PermitLimit = 3,
        WindowSeconds = 60,
    };

    private static (InMemoryRateLimitCounter Counter, FakeTimeProvider Time) Create()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 29, 12, 0, 0, TimeSpan.Zero));
        return (new InMemoryRateLimitCounter(time), time);
    }

    private static async Task<RateLimitResult> AcquireAsync(
        InMemoryRateLimitCounter counter,
        string key = "client-1") =>
        await counter.TryAcquireAsync(key, Policy, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Requests_within_limit_are_allowed()
    {
        var (counter, _) = Create();

        for (var i = 0; i < Policy.PermitLimit; i++)
        {
            Assert.True((await AcquireAsync(counter)).Allowed);
        }
    }

    [Fact]
    public async Task Request_over_limit_is_rejected()
    {
        var (counter, _) = Create();

        for (var i = 0; i < Policy.PermitLimit; i++)
        {
            await AcquireAsync(counter);
        }

        var rejected = await AcquireAsync(counter);

        Assert.False(rejected.Allowed);
        Assert.Equal(0, rejected.Remaining);
        Assert.True(rejected.RetryAfter > TimeSpan.Zero);
    }

    [Fact]
    public async Task Partitions_are_counted_independently()
    {
        var (counter, _) = Create();

        for (var i = 0; i < Policy.PermitLimit; i++)
        {
            await AcquireAsync(counter, "client-1");
        }

        Assert.False((await AcquireAsync(counter, "client-1")).Allowed);

        // Лимит одного клиента не должен влиять на другого.
        Assert.True((await AcquireAsync(counter, "client-2")).Allowed);
    }

    [Fact]
    public async Task Previous_window_decays_gradually()
    {
        var (counter, time) = Create();

        for (var i = 0; i < Policy.PermitLimit; i++)
        {
            await AcquireAsync(counter);
        }

        Assert.False((await AcquireAsync(counter)).Allowed);

        // Ровно на границе окна прошлая нагрузка учитывается почти полностью,
        // поэтому запрос всё ещё отклоняется — в этом смысл скользящего окна.
        time.Advance(TimeSpan.FromSeconds(60));
        Assert.False((await AcquireAsync(counter)).Allowed);

        // К концу следующего окна вес прошлого падает и запросы снова проходят.
        time.Advance(TimeSpan.FromSeconds(59));
        Assert.True((await AcquireAsync(counter)).Allowed);
    }

    [Fact]
    public async Task Long_idle_period_fully_resets_the_window()
    {
        var (counter, time) = Create();

        for (var i = 0; i < Policy.PermitLimit; i++)
        {
            await AcquireAsync(counter);
        }

        // Пропуск более одного окна: прошлая нагрузка уже не влияет.
        time.Advance(TimeSpan.FromSeconds(180));

        var result = await AcquireAsync(counter);

        Assert.True(result.Allowed);
        Assert.Equal(Policy.PermitLimit - 1, result.Remaining);
    }
}
