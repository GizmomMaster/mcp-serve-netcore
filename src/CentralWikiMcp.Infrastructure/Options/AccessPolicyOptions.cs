namespace CentralWikiMcp.Infrastructure.Options;

/// <summary>
/// Политики доступа к разделам wiki (FR-44).
/// </summary>
public sealed class AccessPolicyOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "AccessPolicy";

    /// <summary>
    /// Группы, которым доступны все разделы (администраторы, раздел 8.3).
    /// </summary>
    public IList<string> FullAccessGroups { get; init; } = [];

    /// <summary>
    /// Соответствие «раздел wiki — группы, которым он доступен».
    /// Раздел, отсутствующий в словаре, доступен только группам из
    /// <see cref="FullAccessGroups"/> — политика запрещающая по умолчанию.
    /// </summary>
    public Dictionary<string, List<string>> NamespaceGroups { get; init; } = [];

    /// <summary>
    /// Разделы, доступные любому аутентифицированному субъекту.
    /// Анонимный доступ не разрешается никогда (NFR-31).
    /// </summary>
    public IList<string> PublicNamespaces { get; init; } = [];
}
