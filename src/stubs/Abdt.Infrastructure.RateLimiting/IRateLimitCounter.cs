namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Счётчик sliding-window. Реализации не бросают исключений наружу:
/// при недоступности хранилища запрос пропускается (fail-open, FR-66).
/// </summary>
public interface IRateLimitCounter
{
    /// <summary>
    /// Регистрирует попытку запроса и сообщает, разрешена ли она.
    /// </summary>
    /// <param name="partitionKey">Ключ партиции (субъект или IP).</param>
    /// <param name="policy">Параметры окна.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Результат проверки лимита.</returns>
    ValueTask<RateLimitResult> TryAcquireAsync(
        string partitionKey,
        RateLimitPolicy policy,
        CancellationToken cancellationToken);
}

/// <summary>Результат проверки лимита.</summary>
/// <param name="Allowed">Разрешён ли запрос.</param>
/// <param name="Limit">Действующий лимит в окне.</param>
/// <param name="Used">Оценка использованных запросов в скользящем окне.</param>
/// <param name="RetryAfter">Через сколько имеет смысл повторить.</param>
public readonly record struct RateLimitResult(
    bool Allowed,
    int Limit,
    int Used,
    TimeSpan RetryAfter)
{
    /// <summary>Сколько запросов осталось в окне.</summary>
    public int Remaining => Math.Max(0, Limit - Used);
}
