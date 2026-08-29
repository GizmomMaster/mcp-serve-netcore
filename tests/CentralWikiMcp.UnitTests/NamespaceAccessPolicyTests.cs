using Xunit;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using CentralWikiMcp.Infrastructure.Security;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки политики доступа к разделам wiki (FR-44, UC-4, NFR-31).
/// </summary>
public sealed class NamespaceAccessPolicyTests
{
    private static NamespaceAccessPolicy CreatePolicy() =>
        new(Options.Create(new AccessPolicyOptions
        {
            FullAccessGroups = { "wiki-admins" },
            PublicNamespaces = { "general" },
            NamespaceGroups =
            {
                ["runbooks"] = ["harness", "developers"],
                ["internal"] = ["developers"],
            },
        }));

    [Fact]
    public void Anonymous_subject_has_no_access()
    {
        var policy = CreatePolicy();

        var allowed = policy.GetAllowedNamespaces(WikiSubject.Anonymous);

        Assert.NotNull(allowed);
        Assert.Empty(allowed);
        Assert.False(policy.CanRead(WikiSubject.Anonymous, "general"));
    }

    [Fact]
    public void Admin_group_gets_unrestricted_access()
    {
        var policy = CreatePolicy();
        var admin = new WikiSubject("admin-1", SubjectKind.User, ["wiki-admins"]);

        // null означает отсутствие ограничения по разделам.
        Assert.Null(policy.GetAllowedNamespaces(admin));
        Assert.True(policy.CanRead(admin, "internal"));
        Assert.True(policy.CanRead(admin, "never-declared-namespace"));
    }

    [Fact]
    public void Harness_sees_only_its_namespaces_and_public_ones()
    {
        var policy = CreatePolicy();
        var harness = new WikiSubject("harness-1", SubjectKind.ServiceAccount, ["harness"]);

        var allowed = policy.GetAllowedNamespaces(harness);

        Assert.NotNull(allowed);
        Assert.Contains("runbooks", allowed);
        Assert.Contains("general", allowed);
        Assert.DoesNotContain("internal", allowed);

        Assert.True(policy.CanRead(harness, "runbooks"));
        Assert.False(policy.CanRead(harness, "internal"));
    }

    [Fact]
    public void Unknown_namespace_is_denied_by_default()
    {
        var policy = CreatePolicy();
        var developer = new WikiSubject("dev-1", SubjectKind.User, ["developers"]);

        // Раздел, не объявленный в конфигурации, недоступен без полного доступа.
        Assert.False(policy.CanRead(developer, "secrets"));
    }

    [Fact]
    public void Subject_without_groups_still_reads_public_namespaces()
    {
        var policy = CreatePolicy();
        var guest = new WikiSubject("guest-1", SubjectKind.User, []);

        Assert.True(policy.CanRead(guest, "general"));
        Assert.False(policy.CanRead(guest, "runbooks"));
    }

    [Fact]
    public void Group_matching_is_case_insensitive()
    {
        var policy = CreatePolicy();
        var harness = new WikiSubject("harness-1", SubjectKind.ServiceAccount, ["HARNESS"]);

        Assert.True(policy.CanRead(harness, "runbooks"));
    }
}
