using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure.Security;

/// <summary>
/// Проверка прав по разделам wiki (FR-11, FR-21, FR-44).
/// Политика запрещающая по умолчанию: без явного разрешения доступа нет.
/// </summary>
internal sealed class NamespaceAccessPolicy(IOptions<AccessPolicyOptions> options)
    : IAccessPolicy
{
    private readonly AccessPolicyOptions options = options.Value;

    /// <inheritdoc />
    public IReadOnlyCollection<string>? GetAllowedNamespaces(WikiSubject subject)
    {
        ArgumentNullException.ThrowIfNull(subject);

        // NFR-31: анонимный доступ к контенту запрещён.
        if (subject.Kind == SubjectKind.Anonymous)
        {
            return [];
        }

        if (HasFullAccess(subject))
        {
            // null означает «без ограничения по разделам».
            return null;
        }

        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var publicNamespace in options.PublicNamespaces)
        {
            allowed.Add(publicNamespace);
        }

        foreach (var (wikiNamespace, groups) in options.NamespaceGroups)
        {
            if (groups.Any(group => subject.Groups.Contains(group, StringComparer.OrdinalIgnoreCase)))
            {
                allowed.Add(wikiNamespace);
            }
        }

        return allowed;
    }

    /// <inheritdoc />
    public bool CanRead(WikiSubject subject, string wikiNamespace)
    {
        ArgumentNullException.ThrowIfNull(subject);
        ArgumentException.ThrowIfNullOrWhiteSpace(wikiNamespace);

        var allowed = GetAllowedNamespaces(subject);

        return allowed is null
            || allowed.Contains(wikiNamespace, StringComparer.OrdinalIgnoreCase);
    }

    private bool HasFullAccess(WikiSubject subject) =>
        options.FullAccessGroups.Any(
            group => subject.Groups.Contains(group, StringComparer.OrdinalIgnoreCase));
}
