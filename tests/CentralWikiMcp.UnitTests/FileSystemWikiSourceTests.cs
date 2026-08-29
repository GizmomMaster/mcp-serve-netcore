using Xunit;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using CentralWikiMcp.Infrastructure.Sources;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки файлового источника wiki (FR-30, FR-36, NFR-34).
/// </summary>
public sealed class FileSystemWikiSourceTests : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("wiki-source-tests").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private FileSystemWikiSource CreateSource(int maxFileSizeBytes = 5 * 1024 * 1024) =>
        new(
            Options.Create(new WikiSourceOptions
            {
                RootPath = root,
                SourceId = "test",
                MaxFileSizeBytes = maxFileSizeBytes,
                DefaultNamespace = "general",
            }),
            NullLogger<FileSystemWikiSource>.Instance);

    private void WriteFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
    }

    private async Task<List<WikiPage>> ReadAllAsync(FileSystemWikiSource source)
    {
        var pages = new List<WikiPage>();

        await foreach (var page in source.EnumeratePagesAsync(TestContext.Current.CancellationToken))
        {
            pages.Add(page);
        }

        return pages;
    }

    [Fact]
    public async Task Front_matter_defines_title_namespace_and_tags()
    {
        WriteFile(
            "runbooks/deploy.md",
            """
            ---
            title: Deployment runbook
            namespace: runbooks
            tags:
              - deploy
              - harness
            updatedAt: 2026-08-01T10:15:00Z
            ---

            # Заголовок из тела

            Текст страницы.
            """);

        var pages = await ReadAllAsync(CreateSource());

        var page = Assert.Single(pages);
        Assert.Equal("Deployment runbook", page.Title);
        Assert.Equal("runbooks", page.Namespace);
        Assert.Equal(["deploy", "harness"], page.Tags);
        Assert.Equal("runbooks/deploy", page.Path);
        Assert.Equal(
            new DateTimeOffset(2026, 8, 1, 10, 15, 0, TimeSpan.Zero),
            page.UpdatedAt);

        // Заголовок front matter уже снят, в содержимом остаётся только тело.
        Assert.DoesNotContain("title: Deployment runbook", page.ContentMarkdown, StringComparison.Ordinal);
        Assert.Contains("Текст страницы.", page.ContentMarkdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Namespace_falls_back_to_first_path_segment()
    {
        WriteFile("internal/architecture.md", "# Архитектура\n\nОписание.");

        var pages = await ReadAllAsync(CreateSource());

        var page = Assert.Single(pages);
        Assert.Equal("internal", page.Namespace);
        Assert.Equal("Архитектура", page.Title);
    }

    [Fact]
    public async Task Root_level_file_gets_default_namespace()
    {
        WriteFile("readme.md", "# Корневая страница");

        var pages = await ReadAllAsync(CreateSource());

        var page = Assert.Single(pages);
        Assert.Equal("general", page.Namespace);
    }

    [Fact]
    public async Task Revision_changes_only_when_content_changes()
    {
        WriteFile("general/page.md", "# Страница\n\nПервая версия.");
        var first = Assert.Single(await ReadAllAsync(CreateSource()));

        var unchanged = Assert.Single(await ReadAllAsync(CreateSource()));
        Assert.Equal(first.Revision, unchanged.Revision);
        Assert.Equal(first.PageId, unchanged.PageId);

        WriteFile("general/page.md", "# Страница\n\nВторая версия.");
        var changed = Assert.Single(await ReadAllAsync(CreateSource()));

        Assert.NotEqual(first.Revision, changed.Revision);

        // Идентификатор считается от пути, поэтому правка текста его не меняет (FR-36).
        Assert.Equal(first.PageId, changed.PageId);
    }

    [Fact]
    public async Task Files_larger_than_limit_are_skipped()
    {
        WriteFile("general/big.md", new string('x', 4096));
        WriteFile("general/small.md", "# Небольшая страница");

        var pages = await ReadAllAsync(CreateSource(maxFileSizeBytes: 2048));

        var page = Assert.Single(pages);
        Assert.Equal("general/small", page.Path);
    }

    [Fact]
    public async Task Non_markdown_files_are_ignored()
    {
        WriteFile("general/notes.txt", "не страница wiki");
        WriteFile("general/image.png", "binary");
        WriteFile("general/page.md", "# Страница");

        var pages = await ReadAllAsync(CreateSource());

        var page = Assert.Single(pages);
        Assert.Equal("general/page", page.Path);
    }

    [Fact]
    public async Task Symlink_escaping_root_is_not_followed()
    {
        // NFR-34: симлинк наружу не должен утащить в индекс файлы вне корня wiki.
        var outside = Directory.CreateTempSubdirectory("wiki-outside");

        try
        {
            var secretPath = Path.Combine(outside.FullName, "secret.md");
            await File.WriteAllTextAsync(
                secretPath,
                "# Секрет",
                TestContext.Current.CancellationToken);

            WriteFile("general/page.md", "# Обычная страница");

            var linkPath = Path.Combine(root, "general", "linked.md");
            File.CreateSymbolicLink(linkPath, secretPath);

            var pages = await ReadAllAsync(CreateSource());

            var page = Assert.Single(pages);
            Assert.Equal("general/page", page.Path);
        }
        finally
        {
            outside.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Broken_front_matter_does_not_break_indexing()
    {
        WriteFile(
            "general/broken.md",
            """
            ---
            title: [ незакрытая скобка
              tags: - плохой отступ
            ---

            # Тело страницы
            """);

        var pages = await ReadAllAsync(CreateSource());

        // Страница всё равно попадает в индекс, просто без метаданных из заголовка.
        var page = Assert.Single(pages);
        Assert.Equal("general/broken", page.Path);
    }

    [Fact]
    public async Task Missing_root_yields_no_pages()
    {
        var source = new FileSystemWikiSource(
            Options.Create(new WikiSourceOptions
            {
                RootPath = Path.Combine(root, "does-not-exist"),
                SourceId = "test",
                DefaultNamespace = "general",
            }),
            NullLogger<FileSystemWikiSource>.Instance);

        var pages = await ReadAllAsync(source);

        Assert.Empty(pages);
    }
}
