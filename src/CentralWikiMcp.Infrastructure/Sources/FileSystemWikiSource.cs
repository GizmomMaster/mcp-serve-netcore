using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using CentralWikiMcp.Domain.Abstractions;
using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Infrastructure.Sources;

/// <summary>
/// Источник wiki из папки с Markdown-файлами (FR-30).
/// Раздел страницы определяется первым сегментом относительного пути,
/// ревизия — хешем содержимого, что даёт инкрементальную синхронизацию (FR-36).
/// </summary>
internal sealed partial class FileSystemWikiSource(
    IOptions<WikiSourceOptions> options,
    ILogger<FileSystemWikiSource> logger) : IWikiSource
{
    private readonly WikiSourceOptions options = options.Value;

    /// <inheritdoc />
    public string SourceId => options.SourceId;

    /// <inheritdoc />
    public async IAsyncEnumerable<WikiPage> EnumeratePagesAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var root = ResolveRoot();

        if (!Directory.Exists(root))
        {
            LogRootMissing(logger, root);
            yield break;
        }

        var extensions = options.Extensions
            .Select(extension => extension.StartsWith('.') ? extension : "." + extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,

            // NFR-34: симлинк наружу увёл бы индексацию за пределы корня.
            AttributesToSkip = FileAttributes.Hidden
                | FileAttributes.System
                | FileAttributes.ReparsePoint,
        };

        foreach (var file in Directory.EnumerateFiles(root, "*", enumerationOptions))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!extensions.Contains(Path.GetExtension(file)))
            {
                continue;
            }

            var page = await TryReadPageAsync(root, file, cancellationToken).ConfigureAwait(false);
            if (page is not null)
            {
                yield return page;
            }
        }
    }

    /// <summary>
    /// Приводит корень к абсолютному нормализованному пути.
    /// Дальше все пути сверяются с ним, чтобы выход за корень был невозможен (NFR-34).
    /// </summary>
    private string ResolveRoot() => Path.GetFullPath(options.RootPath);

    private async Task<WikiPage?> TryReadPageAsync(
        string root,
        string file,
        CancellationToken cancellationToken)
    {
        try
        {
            var fullPath = Path.GetFullPath(file);

            // NFR-34: страховка на случай, если перечисление вернуло путь вне корня.
            if (!IsInsideRoot(root, fullPath))
            {
                LogOutsideRoot(logger, fullPath);
                return null;
            }

            var info = new FileInfo(fullPath);
            if (info.Length > options.MaxFileSizeBytes)
            {
                LogFileTooLarge(logger, fullPath, info.Length);
                return null;
            }

            var content = await File
                .ReadAllTextAsync(fullPath, Encoding.UTF8, cancellationToken)
                .ConfigureAwait(false);

            return BuildPage(root, fullPath, content, info.LastWriteTimeUtc);
        }
        catch (IOException ex)
        {
            LogReadFailed(logger, file, ex.Message);
            return null;
        }
        catch (UnauthorizedAccessException ex)
        {
            LogReadFailed(logger, file, ex.Message);
            return null;
        }
    }

    private WikiPage BuildPage(
        string root,
        string fullPath,
        string content,
        DateTime lastWriteUtc)
    {
        var (frontMatter, body) = MarkdownFrontMatter.Split(content);

        var relativePath = Path.GetRelativePath(root, fullPath)
            .Replace(Path.DirectorySeparatorChar, '/');

        var logicalPath = relativePath[..^Path.GetExtension(relativePath).Length];

        var wikiNamespace = !string.IsNullOrWhiteSpace(frontMatter?.Namespace)
            ? frontMatter.Namespace.Trim()
            : DeriveNamespace(logicalPath);

        var title = !string.IsNullOrWhiteSpace(frontMatter?.Title)
            ? frontMatter.Title.Trim()
            : ExtractTitle(body) ?? Path.GetFileNameWithoutExtension(fullPath);

        var tags = frontMatter?.Tags is { Count: > 0 } frontMatterTags
            ? frontMatterTags
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .ToArray()
            : [];

        var updatedAt = frontMatter?.ParseUpdatedAt()
            ?? new DateTimeOffset(lastWriteUtc, TimeSpan.Zero);

        return new WikiPage(
            PageId: ComputePageId(logicalPath),
            Path: logicalPath,
            Title: title,
            ContentMarkdown: body,
            Namespace: wikiNamespace,
            Tags: tags,
            UpdatedAt: updatedAt,
            Revision: ComputeRevision(content),
            Source: options.SourceId);
    }

    private string DeriveNamespace(string logicalPath)
    {
        var separator = logicalPath.IndexOf('/', StringComparison.Ordinal);
        return separator > 0 ? logicalPath[..separator] : options.DefaultNamespace;
    }

    private static string? ExtractTitle(string body)
    {
        foreach (var line in body.Split('\n', StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                return line[2..].Trim();
            }

            if (line.Length > 0)
            {
                // Заголовок ищем только до первого содержательного текста.
                break;
            }
        }

        return null;
    }

    private static bool IsInsideRoot(string root, string candidate)
    {
        var normalizedRoot = root.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        return candidate.StartsWith(normalizedRoot, StringComparison.Ordinal);
    }

    /// <summary>Идентификатор страницы стабилен между запусками и не зависит от машины.</summary>
    private static string ComputePageId(string logicalPath) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(logicalPath)))[..16];

    private static string ComputeRevision(string content) =>
        Convert.ToHexStringLower(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)))[..16];

    [LoggerMessage(
        EventId = 3000,
        Level = LogLevel.Error,
        Message = "Корневая папка wiki не найдена: {Root}")]
    private static partial void LogRootMissing(ILogger logger, string root);

    [LoggerMessage(
        EventId = 3001,
        Level = LogLevel.Warning,
        Message = "Файл вне корня wiki пропущен: {Path}")]
    private static partial void LogOutsideRoot(ILogger logger, string path);

    [LoggerMessage(
        EventId = 3002,
        Level = LogLevel.Warning,
        Message = "Файл пропущен, превышен лимит размера: {Path} ({Length} байт)")]
    private static partial void LogFileTooLarge(ILogger logger, string path, long length);

    [LoggerMessage(
        EventId = 3003,
        Level = LogLevel.Warning,
        Message = "Не удалось прочитать файл {Path}: {Reason}")]
    private static partial void LogReadFailed(ILogger logger, string path, string reason);
}
