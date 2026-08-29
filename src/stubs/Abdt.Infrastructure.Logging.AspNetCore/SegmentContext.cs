namespace Abdt.Infrastructure.Logging.AspNetCore;

/// <summary>
/// Сегментный контекст запроса (NFR-41, FR-51): сквозная корреляция между сервисами.
/// </summary>
public sealed class SegmentContext
{
    /// <summary>Бизнес-операция, сквозная через всю цепочку сервисов.</summary>
    public string BsnOperationId { get; set; } = string.Empty;

    /// <summary>Идентификатор конкретного HTTP-запроса.</summary>
    public string RequestId { get; set; } = string.Empty;

    /// <summary>Система-источник запроса.</summary>
    public string Origin { get; set; } = string.Empty;

    /// <summary>Субъект (сервисный аккаунт или пользователь), от имени которого идёт запрос.</summary>
    public string? Subject { get; set; }
}

/// <summary>
/// Доступ к <see cref="SegmentContext"/> текущего запроса.
/// </summary>
public interface ISegmentContextAccessor
{
    /// <summary>Контекст текущего запроса; <c>null</c> вне области запроса.</summary>
    SegmentContext? Current { get; set; }
}

/// <inheritdoc />
public sealed class SegmentContextAccessor : ISegmentContextAccessor
{
    private static readonly AsyncLocal<SegmentContext?> Storage = new();

    /// <inheritdoc />
    public SegmentContext? Current
    {
        get => Storage.Value;
        set => Storage.Value = value;
    }
}
