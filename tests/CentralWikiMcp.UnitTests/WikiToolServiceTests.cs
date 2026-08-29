using Xunit;
using Abdt.Infrastructure.Logging.AspNetCore;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using CentralWikiMcp.Infrastructure.Security;
using CentralWikiMcp.Server.Contracts;
using CentralWikiMcp.Server.Options;
using CentralWikiMcp.Server.Security;
using CentralWikiMcp.Server.Tools;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки прикладного слоя инструментов: права, лимиты, аудит
/// (FR-11, FR-12, FR-21, FR-24, FR-45, FR-53, FR-61, FR-62).
/// </summary>
public sealed class WikiToolServiceTests
{
    private static readonly WikiSubject Harness =
        new("harness-1", SubjectKind.ServiceAccount, ["harness"]);

    private sealed class StaticSubjectAccessor(WikiSubject subject) : ISubjectAccessor
    {
        public WikiSubject Current { get; } = subject;
    }

    private static WikiPage CreatePage(
        string path,
        string wikiNamespace,
        string content = "Содержимое страницы.") =>
        new(
            PageId: path.Replace('/', '-'),
            Path: path,
            Title: path,
            ContentMarkdown: content,
            Namespace: wikiNamespace,
            Tags: [],
            UpdatedAt: new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            Revision: "rev1",
            Source: "test");

    private static (WikiToolService Service, FakeWikiIndex Index, FakeAuditSink Audit)
        Create(WikiSubject? subject = null, WikiToolOptions? toolOptions = null)
    {
        var index = new FakeWikiIndex();
        var audit = new FakeAuditSink();

        var options = Options.Create(toolOptions ?? new WikiToolOptions());

        var accessPolicy = new NamespaceAccessPolicy(Options.Create(new AccessPolicyOptions
        {
            FullAccessGroups = { "wiki-admins" },
            PublicNamespaces = { "general" },
            NamespaceGroups = { ["runbooks"] = ["harness"], ["internal"] = ["developers"] },
        }));

        var auditor = new ToolAuditor(
            audit,
            new SegmentContextAccessor(),
            new FakeTimeProvider(),
            NullLogger<ToolAuditor>.Instance);

        var service = new WikiToolService(
            index,
            accessPolicy,
            new StaticSubjectAccessor(subject ?? Harness),
            auditor,
            new WikiSearchQueryValidator(options),
            options);

        return (service, index, audit);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Search_clamps_top_k_to_server_maximum()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { MaxTopK = 10, DefaultTopK = 5 });

        // FR-12, FR-62: клиент не может выпросить больше, чем разрешил сервер.
        await service.SearchAsync("запрос", topK: 1000, filters: null, Token);

        Assert.Equal(10, index.ReceivedQueries[0].TopK);
    }

    [Fact]
    public async Task Search_uses_default_top_k_when_not_specified()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { DefaultTopK = 7, MaxTopK = 25 });

        await service.SearchAsync("запрос", topK: null, filters: null, Token);

        Assert.Equal(7, index.ReceivedQueries[0].TopK);
    }

    [Fact]
    public async Task Search_passes_only_allowed_namespaces_to_index()
    {
        var (service, index, _) = Create();

        await service.SearchAsync("запрос", null, null, Token);

        var allowed = index.ReceivedAllowedNamespaces[0];

        Assert.NotNull(allowed);
        Assert.Contains("runbooks", allowed);
        Assert.Contains("general", allowed);
        Assert.DoesNotContain("internal", allowed);
    }

    [Fact]
    public async Task Search_by_anonymous_subject_is_denied_and_audited()
    {
        var (service, _, audit) = Create(WikiSubject.Anonymous);

        await Assert.ThrowsAsync<WikiAccessDeniedException>(
            () => service.SearchAsync("запрос", null, null, Token));

        // UC-4: отказ обязан оставить след в аудите.
        var recorded = Assert.Single(audit.Events);
        Assert.Equal(AuditOutcome.Denied, recorded.Outcome);
        Assert.Equal(WikiToolNames.Search, recorded.Tool);
    }

    [Fact]
    public async Task Empty_search_query_fails_validation()
    {
        var (service, _, _) = Create();

        await Assert.ThrowsAsync<ValidationException>(
            () => service.SearchAsync("   ", null, null, Token));
    }

    [Fact]
    public async Task Successful_search_is_audited_as_allowed()
    {
        var (service, index, audit) = Create();
        index.Add(CreatePage("runbooks/deploy", "runbooks"));

        var response = await service.SearchAsync("deploy", null, null, Token);

        Assert.Single(response.Results);
        Assert.Equal(AuditOutcome.Allowed, Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public async Task Audit_masks_sensitive_parameters()
    {
        var (service, _, audit) = Create();

        // Поисковый запрос попадает в аудит, поэтому ПД внутри него маскируются.
        await service.SearchAsync("напишите ivan@example.com", null, null, Token);

        var recorded = Assert.Single(audit.Events);
        Assert.DoesNotContain("ivan@example.com", recorded.SafeParameters, StringComparison.Ordinal);
        Assert.NotEmpty(recorded.ParametersHash);
    }

    [Fact]
    public async Task Get_page_from_forbidden_namespace_is_denied()
    {
        var (service, index, audit) = Create();
        index.Add(CreatePage("internal/architecture", "internal"));

        await Assert.ThrowsAsync<WikiAccessDeniedException>(
            () => service.GetPageAsync("internal/architecture", null, Token));

        var recorded = Assert.Single(audit.Events);
        Assert.Equal(AuditOutcome.Denied, recorded.Outcome);
        Assert.Equal("internal/architecture", recorded.TargetPath);
    }

    [Fact]
    public async Task Missing_page_is_reported_as_not_found()
    {
        var (service, _, audit) = Create();

        await Assert.ThrowsAsync<WikiPageNotFoundException>(
            () => service.GetPageAsync("runbooks/absent", null, Token));

        Assert.Equal(AuditOutcome.NotFound, Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public async Task Large_page_is_returned_in_chunks()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { PageChunkChars = 1000, MaxResponseChars = 100_000 });

        index.Add(CreatePage("runbooks/big", "runbooks", new string('a', 2500)));

        // FR-24: страница крупнее лимита отдаётся частями.
        var first = await service.GetPageAsync("runbooks/big", null, Token);

        Assert.True(first.Truncated);
        Assert.Equal(1000, first.ContentMarkdown.Length);
        Assert.Equal(0, first.Offset);
        Assert.Equal(2500, first.TotalChars);
        Assert.Equal(1000, first.NextOffset);

        var second = await service.GetPageAsync("runbooks/big", first.NextOffset, Token);
        Assert.Equal(1000, second.ContentMarkdown.Length);
        Assert.Equal(2000, second.NextOffset);

        var third = await service.GetPageAsync("runbooks/big", second.NextOffset, Token);
        Assert.False(third.Truncated);
        Assert.Equal(500, third.ContentMarkdown.Length);
        Assert.Null(third.NextOffset);
    }

    [Fact]
    public async Task Small_page_is_returned_whole()
    {
        var (service, index, _) = Create();
        index.Add(CreatePage("runbooks/small", "runbooks", "Короткий текст."));

        var page = await service.GetPageAsync("runbooks/small", null, Token);

        Assert.False(page.Truncated);
        Assert.Null(page.NextOffset);
        Assert.Equal("Короткий текст.", page.ContentMarkdown);
    }

    [Fact]
    public async Task Offset_beyond_content_returns_empty_chunk()
    {
        var (service, index, _) = Create();
        index.Add(CreatePage("runbooks/small", "runbooks", "Короткий текст."));

        var page = await service.GetPageAsync("runbooks/small", offset: 10_000, Token);

        Assert.Empty(page.ContentMarkdown);
        Assert.False(page.Truncated);
    }

    [Fact]
    public async Task List_pages_reports_more_results_without_extra_query()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { MaxListPageSize = 100 });

        for (var i = 0; i < 5; i++)
        {
            index.Add(CreatePage($"runbooks/page-{i}", "runbooks"));
        }

        var page = await service.ListPagesAsync(null, skip: 0, take: 2, Token);

        Assert.Equal(2, page.Pages.Count);
        Assert.True(page.HasMore);

        var last = await service.ListPagesAsync(null, skip: 4, take: 2, Token);
        Assert.Single(last.Pages);
        Assert.False(last.HasMore);
    }

    [Fact]
    public async Task List_pages_clamps_take_to_server_maximum()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { MaxListPageSize = 3 });

        for (var i = 0; i < 10; i++)
        {
            index.Add(CreatePage($"runbooks/page-{i}", "runbooks"));
        }

        var page = await service.ListPagesAsync(null, skip: 0, take: 1000, Token);

        Assert.Equal(3, page.Pages.Count);
        Assert.Equal(3, page.Take);
    }

    [Fact]
    public async Task List_pages_hides_forbidden_namespaces()
    {
        var (service, index, _) = Create();
        index.Add(CreatePage("runbooks/deploy", "runbooks"));
        index.Add(CreatePage("internal/architecture", "internal"));

        var page = await service.ListPagesAsync(null, null, null, Token);

        var single = Assert.Single(page.Pages);
        Assert.Equal("runbooks/deploy", single.Path);
    }

    [Fact]
    public async Task Admin_sees_every_namespace()
    {
        var admin = new WikiSubject("admin-1", SubjectKind.User, ["wiki-admins"]);
        var (service, index, _) = Create(admin);

        index.Add(CreatePage("internal/architecture", "internal"));

        var page = await service.GetPageAsync("internal/architecture", null, Token);

        Assert.Equal("internal/architecture", page.Path);
    }

    private const string RunbookContent = """
        # Развёртывание сервиса

        Порядок выкладки.

        ## Предусловия

        Собран образ.

        ## Откат

        Вернуть предыдущий тег образа.
        """;

    [Fact]
    public async Task Get_section_returns_matching_section_content()
    {
        var (service, index, _) = Create();
        index.Add(CreatePage("runbooks/deploy", "runbooks", RunbookContent));

        var section = await service.GetSectionAsync("runbooks/deploy", "откат", Token);

        Assert.Equal("Откат", section.SectionTitle);
        Assert.Contains("Вернуть предыдущий тег образа.", section.ContentMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Собран образ.", section.ContentMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Get_section_missing_heading_is_reported_as_not_found()
    {
        var (service, index, audit) = Create();
        index.Add(CreatePage("runbooks/deploy", "runbooks", RunbookContent));

        await Assert.ThrowsAsync<WikiSectionNotFoundException>(
            () => service.GetSectionAsync("runbooks/deploy", "Несуществующая секция", Token));

        Assert.Equal(AuditOutcome.NotFound, Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public async Task Get_section_from_forbidden_namespace_is_denied()
    {
        var (service, index, audit) = Create();
        index.Add(CreatePage("internal/architecture", "internal", RunbookContent));

        await Assert.ThrowsAsync<WikiAccessDeniedException>(
            () => service.GetSectionAsync("internal/architecture", "Откат", Token));

        Assert.Equal(AuditOutcome.Denied, Assert.Single(audit.Events).Outcome);
    }

    [Fact]
    public async Task Recent_changes_are_sorted_by_updated_at_descending()
    {
        var (service, index, _) = Create();
        var older = CreatePage("runbooks/old", "runbooks") with
        {
            UpdatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
        };
        var newer = CreatePage("runbooks/new", "runbooks") with
        {
            UpdatedAt = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
        };
        index.Add(older);
        index.Add(newer);

        var response = await service.ListRecentChangesAsync(null, Token);

        Assert.Equal(["runbooks/new", "runbooks/old"], response.Changes.Select(c => c.Path));
    }

    [Fact]
    public async Task Recent_changes_clamps_take_to_server_maximum()
    {
        var (service, index, _) = Create(
            toolOptions: new WikiToolOptions { MaxRecentChanges = 2 });

        for (var i = 0; i < 5; i++)
        {
            index.Add(CreatePage($"runbooks/page-{i}", "runbooks"));
        }

        var response = await service.ListRecentChangesAsync(1000, Token);

        Assert.Equal(2, response.Changes.Count);
    }

    [Fact]
    public async Task Recent_changes_hides_forbidden_namespaces()
    {
        var (service, index, _) = Create();
        index.Add(CreatePage("runbooks/deploy", "runbooks"));
        index.Add(CreatePage("internal/architecture", "internal"));

        var response = await service.ListRecentChangesAsync(null, Token);

        var single = Assert.Single(response.Changes);
        Assert.Equal("runbooks/deploy", single.Path);
    }

    [Fact]
    public async Task Recent_changes_denied_when_no_namespace_is_allowed()
    {
        var (service, _, audit) = Create(WikiSubject.Anonymous);

        await Assert.ThrowsAsync<WikiAccessDeniedException>(
            () => service.ListRecentChangesAsync(null, Token));

        Assert.Equal(AuditOutcome.Denied, Assert.Single(audit.Events).Outcome);
    }
}
