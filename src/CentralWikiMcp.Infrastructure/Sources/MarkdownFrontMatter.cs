using System.Globalization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace CentralWikiMcp.Infrastructure.Sources;

/// <summary>
/// YAML-заголовок Markdown-файла: задаёт заголовок, раздел и теги страницы.
/// </summary>
internal sealed class MarkdownFrontMatter
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithNamingConvention(CamelCaseNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Заголовок страницы.</summary>
    public string? Title { get; set; }

    /// <summary>Раздел wiki, к которому относится страница.</summary>
    public string? Namespace { get; set; }

    /// <summary>Теги страницы.</summary>
    public IList<string>? Tags { get; set; }

    /// <summary>Явно заданный момент обновления.</summary>
    public string? UpdatedAt { get; set; }

    /// <summary>Разобранный момент обновления.</summary>
    /// <returns>Значение <see cref="UpdatedAt"/> или <c>null</c>, если оно отсутствует либо нечитаемо.</returns>
    public DateTimeOffset? ParseUpdatedAt() =>
        DateTimeOffset.TryParse(
            UpdatedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var parsed)
            ? parsed
            : null;

    /// <summary>
    /// Отделяет YAML-заголовок от тела документа.
    /// Некорректный YAML не роняет индексацию: файл разбирается как документ без заголовка.
    /// </summary>
    /// <param name="content">Содержимое файла.</param>
    /// <returns>Разобранный заголовок (возможно <c>null</c>) и тело документа.</returns>
    public static (MarkdownFrontMatter? FrontMatter, string Body) Split(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        const string Delimiter = "---";

        var trimmed = content.TrimStart('﻿', ' ', '\t', '\r', '\n');
        if (!trimmed.StartsWith(Delimiter, StringComparison.Ordinal))
        {
            return (null, content);
        }

        var firstLineEnd = trimmed.IndexOf('\n', StringComparison.Ordinal);
        if (firstLineEnd < 0)
        {
            return (null, content);
        }

        var closing = trimmed.IndexOf(
            $"\n{Delimiter}",
            firstLineEnd,
            StringComparison.Ordinal);

        if (closing < 0)
        {
            return (null, content);
        }

        var yaml = trimmed[(firstLineEnd + 1)..closing];

        var afterClosing = trimmed.IndexOf('\n', closing + 1);
        var body = afterClosing < 0 ? string.Empty : trimmed[(afterClosing + 1)..];

        try
        {
            var parsed = Deserializer.Deserialize<MarkdownFrontMatter>(yaml);
            return (parsed, body);
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return (null, content);
        }
    }
}
