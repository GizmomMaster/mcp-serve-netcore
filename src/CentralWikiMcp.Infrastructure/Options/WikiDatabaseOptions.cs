using System.ComponentModel.DataAnnotations;

namespace CentralWikiMcp.Infrastructure.Options;

/// <summary>
/// Настройки подключения к PostgreSQL (NFR-12).
/// </summary>
public sealed class WikiDatabaseOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "Database";

    /// <summary>
    /// Строка подключения. Задаётся только через Vault или переменные окружения (NFR-35),
    /// в appsettings.json не хранится.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>Применять ли миграции при старте сервиса.</summary>
    public bool MigrateOnStartup { get; set; } = true;
}
