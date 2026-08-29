using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Infrastructure.Persistence;

/// <summary>
/// Строка аудита доступа (FR-53).
/// </summary>
internal sealed class AuditEventEntity
{
    /// <summary>Идентификатор события.</summary>
    public Guid Id { get; set; }

    /// <summary>Момент события.</summary>
    public DateTimeOffset OccurredAt { get; set; }

    /// <summary>Кто обращался.</summary>
    public required string SubjectId { get; set; }

    /// <summary>Тип субъекта.</summary>
    public SubjectKind SubjectKind { get; set; }

    /// <summary>Инструмент MCP.</summary>
    public required string Tool { get; set; }

    /// <summary>Целевой путь или раздел.</summary>
    public string? TargetPath { get; set; }

    /// <summary>Итог обращения.</summary>
    public AuditOutcome Outcome { get; set; }

    /// <summary>Параметры без чувствительных данных (FR-52).</summary>
    public required string SafeParameters { get; set; }

    /// <summary>Хеш исходных параметров.</summary>
    public required string ParametersHash { get; set; }

    /// <summary>Сквозной идентификатор бизнес-операции.</summary>
    public string? BsnOperationId { get; set; }

    /// <summary>Идентификатор HTTP-запроса.</summary>
    public string? RequestId { get; set; }

    /// <summary>Длительность обработки в миллисекундах.</summary>
    public long DurationMs { get; set; }
}
