using CentralWikiMcp.Server.Tools;
using Xunit;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Разбор Markdown-секций для <c>wiki_get_section</c> (Could).
/// </summary>
public sealed class MarkdownSectionsTests
{
    private const string Page = """
        # Развёртывание сервиса

        Порядок выкладки сервиса через harness.

        ## Предусловия

        - Собран container image.
        - Применены миграции.

        ## Шаги

        1. Проверить статус.
        2. Обновить образ.

        ## Откат

        Вернуть предыдущий тег образа.
        """;

    [Fact]
    public void Find_returns_section_up_to_next_heading_of_same_level()
    {
        var section = MarkdownSections.Find(Page, "Предусловия");

        Assert.NotNull(section);
        Assert.Equal("Предусловия", section.Value.Title);
        Assert.Contains("Собран container image.", section.Value.ContentMarkdown, StringComparison.Ordinal);
        Assert.DoesNotContain("Вернуть предыдущий тег образа.", section.Value.ContentMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_matches_title_case_insensitively_and_trims_whitespace()
    {
        var section = MarkdownSections.Find(Page, "  откат  ");

        Assert.NotNull(section);
        Assert.Equal("Откат", section.Value.Title);
    }

    [Fact]
    public void Find_returns_last_section_up_to_end_of_document()
    {
        var section = MarkdownSections.Find(Page, "Откат");

        Assert.NotNull(section);
        Assert.Contains("Вернуть предыдущий тег образа.", section.Value.ContentMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Find_returns_null_for_unknown_heading()
    {
        var section = MarkdownSections.Find(Page, "Несуществующая секция");

        Assert.Null(section);
    }

    [Fact]
    public void Find_returns_top_level_section_spanning_its_subsections()
    {
        var section = MarkdownSections.Find(Page, "Развёртывание сервиса");

        Assert.NotNull(section);
        Assert.Contains("## Откат", section.Value.ContentMarkdown, StringComparison.Ordinal);
    }
}
