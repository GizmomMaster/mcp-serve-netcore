using CentralWikiMcp.Domain.Model;
using CentralWikiMcp.Server.Options;
using FluentValidation;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Server.Tools;

/// <summary>
/// Валидация параметров поиска (FR-04, NFR-32).
/// </summary>
public sealed class WikiSearchQueryValidator : AbstractValidator<WikiSearchQuery>
{
    /// <summary>Максимальная длина поискового запроса.</summary>
    public const int MaxQueryLength = 1000;

    /// <summary>Максимальное число тегов в фильтре.</summary>
    public const int MaxTags = 20;

    /// <summary>
    /// Создаёт валидатор.
    /// </summary>
    /// <param name="options">Серверные лимиты.</param>
    public WikiSearchQueryValidator(IOptions<WikiToolOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        RuleFor(query => query.Query)
            .NotEmpty()
            .WithMessage("Поисковый запрос не должен быть пустым.")
            .MaximumLength(MaxQueryLength)
            .WithMessage($"Длина запроса не должна превышать {MaxQueryLength} символов.");

        RuleFor(query => query.TopK)
            .InclusiveBetween(1, options.Value.MaxTopK)
            .WithMessage($"top_k должен быть в диапазоне 1..{options.Value.MaxTopK}.");

        RuleFor(query => query.Namespace)
            .MaximumLength(256)
            .When(query => query.Namespace is not null);

        RuleFor(query => query.Tags!)
            .Must(tags => tags.Count <= MaxTags)
            .WithMessage($"Число тегов не должно превышать {MaxTags}.")
            .When(query => query.Tags is not null);

        RuleForEach(query => query.Tags!)
            .NotEmpty()
            .MaximumLength(128)
            .When(query => query.Tags is not null);
    }
}
