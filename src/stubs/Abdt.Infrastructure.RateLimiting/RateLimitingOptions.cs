namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Настройки двухуровневого ограничения нагрузки (FR-60, FR-64, FR-65).
/// </summary>
public sealed class RateLimitingOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "RateLimiting";

    /// <summary>Включено ли ограничение.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Строка подключения к Redis. Пусто — счётчики в памяти процесса.</summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>Префикс ключей в Redis.</summary>
    public string KeyPrefix { get; set; } = "rl";

    /// <summary>Общий лимит первого уровня — защита от DDoS.</summary>
    public RateLimitPolicy General { get; set; } = new() { PermitLimit = 600, WindowSeconds = 60 };

    /// <summary>Бизнес-лимиты второго уровня по имени политики.</summary>
    public Dictionary<string, RateLimitPolicy> Policies { get; init; } = [];

    /// <summary>
    /// Партиционирование общего лимита: по <c>ClientId</c> (субъект) или по <c>Ip</c> (FR-64).
    /// </summary>
    public RateLimitPartition Partition { get; set; } = RateLimitPartition.ClientId;
}

/// <summary>Ключ партиционирования лимита.</summary>
public enum RateLimitPartition
{
    /// <summary>По аутентифицированному субъекту, с откатом на IP для анонимных.</summary>
    ClientId = 0,

    /// <summary>По IP-адресу клиента.</summary>
    Ip = 1,
}

/// <summary>Параметры одной политики sliding-window.</summary>
public sealed class RateLimitPolicy
{
    /// <summary>Разрешённое число запросов в окне.</summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>Длительность окна в секундах.</summary>
    public int WindowSeconds { get; set; } = 60;
}
