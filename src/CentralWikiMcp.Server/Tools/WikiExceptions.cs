namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Доступ к запрошенному контенту запрещён (UC-4, FR-45).
/// </summary>
public sealed class WikiAccessDeniedException : Exception
{
    /// <summary>Создаёт исключение с сообщением по умолчанию.</summary>
    public WikiAccessDeniedException()
        : base("Доступ к запрошенному контенту wiki запрещён.")
    {
    }

    /// <summary>Создаёт исключение с указанным сообщением.</summary>
    /// <param name="message">Сообщение.</param>
    public WikiAccessDeniedException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с сообщением и внутренней причиной.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public WikiAccessDeniedException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Запрошенная страница отсутствует в индексе.
/// </summary>
public sealed class WikiPageNotFoundException : Exception
{
    /// <summary>Создаёт исключение с сообщением по умолчанию.</summary>
    public WikiPageNotFoundException()
        : base("Страница wiki не найдена.")
    {
    }

    /// <summary>Создаёт исключение с указанным сообщением.</summary>
    /// <param name="message">Сообщение.</param>
    public WikiPageNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с сообщением и внутренней причиной.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public WikiPageNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Запрошенная секция отсутствует на найденной странице (<c>wiki_get_section</c>, Could).
/// </summary>
public sealed class WikiSectionNotFoundException : Exception
{
    /// <summary>Создаёт исключение с сообщением по умолчанию.</summary>
    public WikiSectionNotFoundException()
        : base("Секция wiki не найдена.")
    {
    }

    /// <summary>Создаёт исключение с указанным сообщением.</summary>
    /// <param name="message">Сообщение.</param>
    public WikiSectionNotFoundException(string message)
        : base(message)
    {
    }

    /// <summary>Создаёт исключение с сообщением и внутренней причиной.</summary>
    /// <param name="message">Сообщение.</param>
    /// <param name="innerException">Внутреннее исключение.</param>
    public WikiSectionNotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
