using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Abdt.Infrastructure.Configuration.Validation;

/// <summary>
/// Валидация конфигурации с ранним падением при ошибках (FR-47).
/// </summary>
public static class ValidationExtensions
{
    /// <summary>
    /// Включает валидацию секции по DataAnnotations и проверку на старте приложения.
    /// Некорректная конфигурация роняет процесс до приёма трафика, а не в рантайме.
    /// </summary>
    /// <typeparam name="TOptions">Тип настроек.</typeparam>
    /// <param name="builder">Билдер настроек.</param>
    /// <returns>Тот же билдер для цепочки вызовов.</returns>
    public static OptionsBuilder<TOptions> WithAbdtValidation<TOptions>(
        this OptionsBuilder<TOptions> builder)
        where TOptions : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder
            .Validate(
                options => Validator.TryValidateObject(
                    options,
                    new ValidationContext(options),
                    validationResults: null,
                    validateAllProperties: true),
                $"Некорректная конфигурация секции {typeof(TOptions).Name}.")
            .ValidateOnStart();
    }
}
