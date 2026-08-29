namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Извлечение одной секции Markdown-страницы по заголовку (<c>wiki_get_section</c>, раздел 11.2, Could).
/// </summary>
public static class MarkdownSections
{
    /// <summary>Найденная секция.</summary>
    /// <param name="Title">Заголовок секции как в исходном тексте.</param>
    /// <param name="ContentMarkdown">Заголовок и тело секции до следующего заголовка того же уровня или выше.</param>
    public readonly record struct Section(string Title, string ContentMarkdown);

    /// <summary>
    /// Ищет секцию по заголовку ATX (<c>#</c>…<c>######</c>) без учёта регистра
    /// и обрамляющих пробелов.
    /// </summary>
    /// <param name="content">Полный текст страницы в Markdown.</param>
    /// <param name="sectionTitle">Искомый заголовок.</param>
    /// <returns>Найденная секция или <c>null</c>, если заголовок не встречается на странице.</returns>
    public static Section? Find(string content, string sectionTitle)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionTitle);

        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var headings = ParseHeadings(lines);

        var targetIndex = headings.FindIndex(heading =>
            string.Equals(heading.Title, sectionTitle.Trim(), StringComparison.OrdinalIgnoreCase));

        if (targetIndex < 0)
        {
            return null;
        }

        var target = headings[targetIndex];

        // Секция заканчивается на следующем заголовке того же уровня или выше
        // (более крупном разделе), а не только на прямом соседе.
        var endLine = lines.Length;
        for (var i = targetIndex + 1; i < headings.Count; i++)
        {
            if (headings[i].Level <= target.Level)
            {
                endLine = headings[i].LineIndex;
                break;
            }
        }

        var sectionText = string.Join('\n', lines[target.LineIndex..endLine]).TrimEnd();
        return new Section(target.Title, sectionText);
    }

    /// <summary>Разбирает строки на заголовки ATX с их уровнем и позицией.</summary>
    private static List<(int Level, string Title, int LineIndex)> ParseHeadings(string[] lines)
    {
        var headings = new List<(int Level, string Title, int LineIndex)>();

        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var level = 0;
            while (level < line.Length && level < 6 && line[level] == '#')
            {
                level++;
            }

            // ATX-заголовок: 1–6 `#`, сразу за которыми обязателен пробел.
            if (level is > 0 and <= 6 && level < line.Length && line[level] == ' ')
            {
                headings.Add((level, line[(level + 1)..].Trim(), i));
            }
        }

        return headings;
    }
}
