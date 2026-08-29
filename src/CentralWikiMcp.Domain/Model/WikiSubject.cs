namespace CentralWikiMcp.Domain.Model;

/// <summary>
/// Субъект, от имени которого выполняется запрос (FR-43: пользователь или сервисный аккаунт).
/// </summary>
/// <param name="Id">Идентификатор субъекта.</param>
/// <param name="Kind">Тип субъекта.</param>
/// <param name="Groups">Группы, по которым вычисляется доступ к разделам wiki (FR-44).</param>
public sealed record WikiSubject(
    string Id,
    SubjectKind Kind,
    IReadOnlyList<string> Groups)
{
    /// <summary>Анонимный субъект: доступа к контенту не имеет (NFR-31).</summary>
    public static WikiSubject Anonymous { get; } =
        new("anonymous", SubjectKind.Anonymous, []);
}

/// <summary>Тип субъекта (FR-43).</summary>
public enum SubjectKind
{
    /// <summary>Не аутентифицирован.</summary>
    Anonymous = 0,

    /// <summary>Живой пользователь-разработчик (раздел 8.2).</summary>
    User = 1,

    /// <summary>Сервисный аккаунт harness (раздел 8.1).</summary>
    ServiceAccount = 2,
}
