using Microsoft.EntityFrameworkCore;

namespace CentralWikiMcp.Infrastructure.Persistence;

/// <summary>
/// Контекст БД сервиса: индекс wiki, аудит и состояние синхронизации (NFR-12).
/// </summary>
/// <param name="options">Настройки контекста.</param>
internal sealed class WikiDbContext(DbContextOptions<WikiDbContext> options)
    : DbContext(options)
{
    /// <summary>Конфигурация текстового поиска PostgreSQL.</summary>
    /// <remarks>
    /// <c>russian</c> даёт стемминг для русскоязычной wiki; для англоязычной
    /// имеет смысл переключить на <c>english</c>.
    /// </remarks>
    public const string TextSearchConfiguration = "russian";

    /// <summary>Проиндексированные страницы.</summary>
    public DbSet<WikiPageEntity> Pages => Set<WikiPageEntity>();

    /// <summary>События аудита.</summary>
    public DbSet<AuditEventEntity> AuditEvents => Set<AuditEventEntity>();

    /// <summary>Состояние синхронизации по источникам.</summary>
    public DbSet<SyncStateEntity> SyncStates => Set<SyncStateEntity>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<WikiPageEntity>(entity =>
        {
            entity.ToTable("wiki_pages");
            entity.HasKey(page => page.PageId);

            entity.Property(page => page.PageId).HasMaxLength(64);
            entity.Property(page => page.Path).HasMaxLength(1024).IsRequired();
            entity.Property(page => page.Title).HasMaxLength(512).IsRequired();
            entity.Property(page => page.Namespace).HasMaxLength(256).IsRequired();
            entity.Property(page => page.Revision).HasMaxLength(64).IsRequired();
            entity.Property(page => page.Source).HasMaxLength(128).IsRequired();
            entity.Property(page => page.ContentMarkdown).IsRequired();

            entity.HasIndex(page => page.Path).IsUnique();
            entity.HasIndex(page => page.Namespace);
            entity.HasIndex(page => page.Source);
            entity.HasIndex(page => page.UpdatedAt);
            entity.HasIndex(page => page.Tags).HasMethod("gin");

            // FR-14: поисковый вектор считает база, приложению не нужно его поддерживать.
            // Заголовок получает вес A, тело — B, поэтому совпадение в заголовке ранжируется выше.
            entity.Property(page => page.SearchVector)
                .HasComputedColumnSql(
                    $"""
                     setweight(to_tsvector('{TextSearchConfiguration}', coalesce("Title", '')), 'A') ||
                     setweight(to_tsvector('{TextSearchConfiguration}', coalesce("ContentMarkdown", '')), 'B')
                     """,
                    stored: true);

            entity.HasIndex(page => page.SearchVector).HasMethod("gin");
        });

        modelBuilder.Entity<AuditEventEntity>(entity =>
        {
            entity.ToTable("audit_events");
            entity.HasKey(auditEvent => auditEvent.Id);

            entity.Property(auditEvent => auditEvent.SubjectId).HasMaxLength(256).IsRequired();
            entity.Property(auditEvent => auditEvent.Tool).HasMaxLength(128).IsRequired();
            entity.Property(auditEvent => auditEvent.TargetPath).HasMaxLength(1024);
            entity.Property(auditEvent => auditEvent.SafeParameters).HasMaxLength(4096).IsRequired();
            entity.Property(auditEvent => auditEvent.ParametersHash).HasMaxLength(64).IsRequired();
            entity.Property(auditEvent => auditEvent.BsnOperationId).HasMaxLength(128);
            entity.Property(auditEvent => auditEvent.RequestId).HasMaxLength(128);

            entity.HasIndex(auditEvent => auditEvent.OccurredAt);
            entity.HasIndex(auditEvent => auditEvent.SubjectId);
            entity.HasIndex(auditEvent => auditEvent.BsnOperationId);
        });

        modelBuilder.Entity<SyncStateEntity>(entity =>
        {
            entity.ToTable("sync_states");
            entity.HasKey(state => state.Source);
            entity.Property(state => state.Source).HasMaxLength(128);
            entity.Property(state => state.Error).HasMaxLength(4096);
        });
    }
}
