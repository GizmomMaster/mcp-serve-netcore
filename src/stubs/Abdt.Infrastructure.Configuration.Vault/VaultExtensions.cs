using Microsoft.Extensions.Configuration;

namespace Abdt.Infrastructure.Configuration.Vault;

/// <summary>
/// Загрузка секретов вне кода и вне appsettings.json (FR-46, NFR-35).
/// </summary>
public static class VaultExtensions
{
    /// <summary>Префикс переменных окружения, из которых читаются секреты.</summary>
    public const string EnvironmentPrefix = "WIKIMCP_";

    /// <summary>
    /// Подключает источник секретов.
    /// В dev-контуре это корпоративный Vault, в prod — Kubernetes Secrets,
    /// которые проецируются в переменные окружения с префиксом <see cref="EnvironmentPrefix"/>.
    /// </summary>
    /// <param name="builder">Билдер конфигурации.</param>
    /// <returns>Тот же билдер для цепочки вызовов.</returns>
    public static IConfigurationBuilder AddVault(this IConfigurationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Переменные окружения перекрывают appsettings.json — секреты в файлы не попадают.
        builder.AddEnvironmentVariables(EnvironmentPrefix);
        return builder;
    }
}
