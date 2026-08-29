using System.ComponentModel.DataAnnotations;

namespace CentralWikiMcp.Infrastructure.Options;

/// <summary>
/// Настройки синхронизации индекса (FR-32).
/// </summary>
public sealed class SyncOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "Sync";

    /// <summary>Запускать ли фоновую синхронизацию по расписанию (FR-31, FR-32).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Интервал между запусками, минуты.</summary>
    [Range(1, 24 * 60)]
    public int IntervalMinutes { get; set; } = 10;

    /// <summary>Запускать ли синхронизацию сразу при старте сервиса.</summary>
    public bool RunOnStartup { get; set; } = true;

    /// <summary>Размер пачки страниц, записываемой в индекс за одну транзакцию.</summary>
    [Range(1, 1000)]
    public int BatchSize { get; set; } = 100;
}
