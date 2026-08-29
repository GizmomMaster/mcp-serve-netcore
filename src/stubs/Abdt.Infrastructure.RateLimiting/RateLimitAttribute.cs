namespace Abdt.Infrastructure.RateLimiting;

/// <summary>
/// Бизнес-лимит для конкретного эндпоинта (FR-65).
/// Применяется как атрибут или через <c>WithMetadata&lt;RateLimitAttribute&gt;()</c>.
/// </summary>
/// <param name="policy">Имя политики из конфигурации <c>RateLimiting:Policies</c>.</param>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class RateLimitAttribute(string policy) : Attribute
{
    /// <summary>Имя политики бизнес-лимита.</summary>
    public string Policy { get; } = policy;
}
