using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Abdt.Infrastructure.Logging.AspNetCore;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using Microsoft.Extensions.Logging;

namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Логирование и аудит вызовов MCP-инструментов (FR-50, FR-51, FR-53).
/// </summary>
public sealed partial class ToolAuditor(
    IAuditSink auditSink,
    ISegmentContextAccessor segmentContextAccessor,
    TimeProvider timeProvider,
    ILogger<ToolAuditor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    /// <summary>
    /// Записывает событие аудита о вызове инструмента.
    /// Сбой записи аудита не должен ломать ответ клиенту, поэтому исключение
    /// подавляется и попадает только в лог.
    /// </summary>
    /// <param name="tool">Имя инструмента.</param>
    /// <param name="subject">Субъект запроса.</param>
    /// <param name="parameters">Параметры вызова.</param>
    /// <param name="targetPath">Целевой путь или раздел.</param>
    /// <param name="outcome">Итог вызова.</param>
    /// <param name="startedTimestamp">Метка времени начала, из <see cref="Stopwatch.GetTimestamp"/>.</param>
    /// <param name="cancellationToken">Токен отмены.</param>
    /// <returns>Задача записи.</returns>
    public async Task RecordAsync(
        string tool,
        WikiSubject subject,
        IReadOnlyDictionary<string, object?> parameters,
        string? targetPath,
        AuditOutcome outcome,
        long startedTimestamp,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tool);
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentNullException.ThrowIfNull(parameters);

        var durationMs = (long)Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
        var context = segmentContextAccessor.Current;

        var safeParameters = BuildSafeParameters(parameters);
        var parametersHash = ComputeHash(parameters);

        var outcomeName = outcome.ToString();
        LogToolInvoked(logger, tool, subject.Id, outcomeName, durationMs, safeParameters);

        var auditEvent = new AuditEvent(
            Id: Guid.NewGuid(),
            OccurredAt: timeProvider.GetUtcNow(),
            SubjectId: subject.Id,
            SubjectKind: subject.Kind,
            Tool: tool,
            TargetPath: targetPath,
            Outcome: outcome,
            SafeParameters: safeParameters,
            ParametersHash: parametersHash,
            BsnOperationId: context?.BsnOperationId,
            RequestId: context?.RequestId,
            DurationMs: durationMs);

        try
        {
            await auditSink.WriteAsync(auditEvent, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogAuditWriteFailed(logger, tool, ex);
        }
    }

    /// <summary>
    /// Сериализует параметры, маскируя чувствительные значения (FR-52).
    /// </summary>
    private static string BuildSafeParameters(IReadOnlyDictionary<string, object?> parameters)
    {
        var safe = new Dictionary<string, object?>(StringComparer.Ordinal);

        foreach (var (key, value) in parameters)
        {
            safe[key] = LogMasking.IsPrivate(key)
                ? LogMasking.Mask
                : LogMasking.MaskFreeText(value?.ToString());
        }

        var json = JsonSerializer.Serialize(safe, JsonOptions);

        return json.Length > 4096 ? json[..4096] : json;
    }

    /// <summary>
    /// Хеш исходных параметров: позволяет сопоставлять повторяющиеся запросы,
    /// не сохраняя сами значения (FR-51).
    /// </summary>
    private static string ComputeHash(IReadOnlyDictionary<string, object?> parameters)
    {
        var ordered = parameters
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => string.Concat(pair.Key, "=", pair.Value?.ToString()));

        var payload = string.Join(";", ordered);

        return Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    [LoggerMessage(
        EventId = 5000,
        Level = LogLevel.Information,
        Message = "MCP tool={Tool} subject={Subject} outcome={Outcome} за {DurationMs} мс, параметры={SafeParameters}")]
    private static partial void LogToolInvoked(
        ILogger logger,
        string tool,
        string subject,
        string outcome,
        long durationMs,
        string safeParameters);

    [LoggerMessage(
        EventId = 5001,
        Level = LogLevel.Error,
        Message = "Не удалось записать аудит вызова инструмента {Tool}")]
    private static partial void LogAuditWriteFailed(ILogger logger, string tool, Exception exception);
}
