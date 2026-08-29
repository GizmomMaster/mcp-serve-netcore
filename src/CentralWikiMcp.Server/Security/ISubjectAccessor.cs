using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Субъект текущего запроса (FR-43).
/// </summary>
public interface ISubjectAccessor
{
    /// <summary>Текущий субъект; для неаутентифицированных — <see cref="WikiSubject.Anonymous"/>.</summary>
    WikiSubject Current { get; }
}
