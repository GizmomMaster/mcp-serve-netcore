namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Запись аудита доступа к wiki (FR-53).
/// </summary>
/// <param name="Id">Идентификатор события.</param>
/// <param name="OccurredAt">Момент события.</param>
/// <param name="SubjectId">Кто обращался.</param>
/// <param name="SubjectKind">Тип субъекта.</param>
/// <param name="Tool">Какой MCP-инструмент вызывался.</param>
/// <param name="TargetPath">К какой странице или разделу шло обращение.</param>
/// <param name="Outcome">Итог обращения.</param>
/// <param name="SafeParameters">Параметры запроса без чувствительных данных (FR-52).</param>
/// <param name="ParametersHash">Хеш исходных параметров для сопоставления повторов (FR-51).</param>
/// <param name="BsnOperationId">Сквозной идентификатор бизнес-операции.</param>
/// <param name="RequestId">Идентификатор HTTP-запроса.</param>
/// <param name="DurationMs">Длительность обработки в миллисекундах.</param>
public sealed record AuditEvent(
    Guid Id,
    DateTimeOffset OccurredAt,
    string SubjectId,
    SubjectKind SubjectKind,
    string Tool,
    string? TargetPath,
    AuditOutcome Outcome,
    string SafeParameters,
    string ParametersHash,
    string? BsnOperationId,
    string? RequestId,
    long DurationMs);

/// <summary>Итог обращения для аудита.</summary>
public enum AuditOutcome
{
    /// <summary>Запрос выполнен, контент отдан.</summary>
    Allowed = 0,

    /// <summary>Отказано по правам доступа (UC-4, FR-45).</summary>
    Denied = 1,

    /// <summary>Запрошенный объект не найден.</summary>
    NotFound = 2,

    /// <summary>Обработка завершилась ошибкой.</summary>
    Error = 3,
}
