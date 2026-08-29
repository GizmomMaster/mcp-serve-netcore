using System.ComponentModel.DataAnnotations;

namespace CentralWikiMcp.Infrastructure.Options;

/// <summary>
/// Настройки файлового источника wiki (FR-30).
/// </summary>
public sealed class WikiSourceOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "WikiSource";

    /// <summary>Корневая папка с Markdown-файлами.</summary>
    [Required(AllowEmptyStrings = false)]
    public string RootPath { get; set; } = string.Empty;

    /// <summary>Идентификатор источника, попадающий в метаданные страниц.</summary>
    [Required(AllowEmptyStrings = false)]
    public string SourceId { get; set; } = "filesystem";

    /// <summary>Расширения файлов, считающихся страницами wiki.</summary>
    public IList<string> Extensions { get; init; } = [".md", ".markdown"];

    /// <summary>
    /// Максимальный размер файла в байтах. Более крупные пропускаются,
    /// чтобы одна страница не выедала память при индексации.
    /// </summary>
    [Range(1024, 100 * 1024 * 1024)]
    public int MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;

    /// <summary>Раздел wiki для файлов в корне, у которых нет своей папки.</summary>
    [Required(AllowEmptyStrings = false)]
    public string DefaultNamespace { get; set; } = "general";
}
