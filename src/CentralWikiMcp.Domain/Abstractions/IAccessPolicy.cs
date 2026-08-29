using CentralWikiMcp.Domain.Model;

namespace CentralWikiMcp.Domain.Abstractions;

/// <summary>
/// Политика доступа к разделам wiki (FR-11, FR-21, FR-44).
/// </summary>
public interface IAccessPolicy
{
    /// <summary>
    /// Возвращает разделы, доступные субъекту на чтение.
    /// </summary>
    /// <param name="subject">Субъект запроса.</param>
    /// <returns>
    /// Множество разрешённых разделов либо <c>null</c>, если субъекту доступны все разделы.
    /// Пустое множество означает отсутствие доступа.
    /// </returns>
    IReadOnlyCollection<string>? GetAllowedNamespaces(WikiSubject subject);

    /// <summary>
    /// Проверяет доступ субъекта к конкретному разделу.
    /// </summary>
    /// <param name="subject">Субъект запроса.</param>
    /// <param name="wikiNamespace">Раздел wiki.</param>
    /// <returns><c>true</c>, если чтение разрешено.</returns>
    bool CanRead(WikiSubject subject, string wikiNamespace);
}
