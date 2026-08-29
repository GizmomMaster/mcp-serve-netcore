using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Контекст одного вызова MCP-инструмента: то, что нужно знать аудиту (FR-53)
/// и что инструмент может уточнить уже по ходу обработки.
/// </summary>
/// <param name="subject">Субъект вызова.</param>
/// <param name="targetPath">Целевой путь или раздел, известный до начала обработки.</param>
internal sealed class ToolCallContext(WikiSubject subject, string? targetPath)
{
    /// <summary>Субъект, от имени которого выполняется вызов.</summary>
    public WikiSubject Subject { get; } = subject;

    /// <summary>
    /// Целевой путь или раздел. Инструмент уточняет значение, когда узнаёт
    /// настоящий путь страницы: в аудит должно попасть то, к чему шло обращение,
    /// а не то, что прислал клиент.
    /// </summary>
    public string? TargetPath { get; set; } = targetPath;
}
