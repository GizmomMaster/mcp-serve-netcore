using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CentralWikiMcp.Infrastructure.Security;

/// <summary>
/// Аудит доступа в отдельной таблице PostgreSQL (FR-53).
/// </summary>
internal sealed class PostgresAuditSink(WikiDbContext dbContext) : IAuditSink
{
    /// <inheritdoc />
    public async Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);

        dbContext.AuditEvents.Add(new AuditEventEntity
        {
            Id = auditEvent.Id,
            OccurredAt = auditEvent.OccurredAt,
            SubjectId = auditEvent.SubjectId,
            SubjectKind = auditEvent.SubjectKind,
            Tool = auditEvent.Tool,
            TargetPath = auditEvent.TargetPath,
            Outcome = auditEvent.Outcome,
            SafeParameters = auditEvent.SafeParameters,
            ParametersHash = auditEvent.ParametersHash,
            BsnOperationId = auditEvent.BsnOperationId,
            RequestId = auditEvent.RequestId,
            DurationMs = auditEvent.DurationMs,
        });

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditEvent>> QueryAsync(
        DateTimeOffset fromUtc,
        DateTimeOffset toUtc,
        int take,
        CancellationToken cancellationToken)
    {
        var events = await dbContext.AuditEvents
            .AsNoTracking()
            .Where(auditEvent => auditEvent.OccurredAt >= fromUtc && auditEvent.OccurredAt <= toUtc)
            .OrderByDescending(auditEvent => auditEvent.OccurredAt)
            .Take(take)
            .Select(auditEvent => new AuditEvent(
                auditEvent.Id,
                auditEvent.OccurredAt,
                auditEvent.SubjectId,
                auditEvent.SubjectKind,
                auditEvent.Tool,
                auditEvent.TargetPath,
                auditEvent.Outcome,
                auditEvent.SafeParameters,
                auditEvent.ParametersHash,
                auditEvent.BsnOperationId,
                auditEvent.RequestId,
                auditEvent.DurationMs))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return events;
    }
}
