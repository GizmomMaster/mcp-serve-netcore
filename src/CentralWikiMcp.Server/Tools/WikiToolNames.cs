namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Имена MCP-инструментов (раздел 11.2). Вынесены в константы,
/// чтобы имя в аудите совпадало с именем, видимым клиенту.
/// </summary>
public static class WikiToolNames
{
    /// <summary>Поиск по wiki.</summary>
    public const string Search = "wiki_search";

    /// <summary>Получение страницы.</summary>
    public const string GetPage = "wiki_get_page";

    /// <summary>Список страниц и разделов.</summary>
    public const string ListPages = "wiki_list_pages";
}
