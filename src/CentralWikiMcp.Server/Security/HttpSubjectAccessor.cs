using System.Security.Claims;
using CentralWikiMcp.Domain.Model;
using Microsoft.AspNetCore.Http;

namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Извлекает субъект из claims текущего HTTP-запроса (FR-43).
/// </summary>
internal sealed class HttpSubjectAccessor(IHttpContextAccessor httpContextAccessor)
    : ISubjectAccessor
{
    /// <summary>Claim, помечающий сервисный аккаунт harness.</summary>
    public const string SubjectKindClaim = "subject_kind";

    /// <summary>Claim с группами доступа.</summary>
    public const string GroupsClaim = "groups";

    /// <inheritdoc />
    public WikiSubject Current
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            if (principal?.Identity is not { IsAuthenticated: true })
            {
                return WikiSubject.Anonymous;
            }

            var id = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? principal.FindFirstValue("sub")
                ?? principal.Identity.Name
                ?? "unknown";

            var kind = ResolveKind(principal);

            var groups = principal.FindAll(GroupsClaim)
                .Concat(principal.FindAll(ClaimTypes.Role))
                .Select(claim => claim.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return new WikiSubject(id, kind, groups);
        }
    }

    private static SubjectKind ResolveKind(ClaimsPrincipal principal)
    {
        var declared = principal.FindFirstValue(SubjectKindClaim);

        if (string.Equals(declared, "service", StringComparison.OrdinalIgnoreCase))
        {
            return SubjectKind.ServiceAccount;
        }

        if (string.Equals(declared, "user", StringComparison.OrdinalIgnoreCase))
        {
            return SubjectKind.User;
        }

        // OAuth2 client_credentials не выдаёт пользовательских claims —
        // отсутствие sub-claim трактуем как сервисный аккаунт.
        return principal.FindFirstValue("sub") is null
            ? SubjectKind.ServiceAccount
            : SubjectKind.User;
    }
}
