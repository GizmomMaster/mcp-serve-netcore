using System.Text.RegularExpressions;

namespace Abdt.Infrastructure.Logging.AspNetCore;

/// <summary>
/// Маскирование персональных и чувствительных данных в логах (FR-52, NFR-38).
/// </summary>
public static partial class LogMasking
{
    /// <summary>
    /// Свойства, значения которых маскируются по умолчанию.
    /// Сравнение регистронезависимое, по вхождению подстроки.
    /// </summary>
    public static IReadOnlyList<string> DefaultPrivateProperties { get; } =
    [
        "token",
        "authorization",
        "password",
        "secret",
        "apikey",
        "api_key",
        "x-api-key",
        "clientsecret",
        "client_secret",
        "connectionstring",
        "cookie",
        "set-cookie",
        "refresh_token",
        "access_token",
        "phone",
        "email",
        "passport",
        "snils",
        "inn",
        "cardnumber",
        "pan",
        "cvv",
    ];

    /// <summary>Значение, подставляемое вместо чувствительных данных.</summary>
    public const string Mask = "***";

    /// <summary>
    /// Проверяет, относится ли свойство к чувствительным.
    /// </summary>
    /// <param name="propertyName">Имя свойства, заголовка или параметра.</param>
    /// <returns><c>true</c>, если значение подлежит маскированию.</returns>
    public static bool IsPrivate(string? propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        foreach (var candidate in DefaultPrivateProperties)
        {
            if (propertyName.Contains(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Маскирует значение, если имя свойства признано чувствительным.
    /// </summary>
    /// <param name="propertyName">Имя свойства.</param>
    /// <param name="value">Исходное значение.</param>
    /// <returns>Исходное или замаскированное значение.</returns>
    public static string? MaskIfPrivate(string? propertyName, string? value) =>
        IsPrivate(propertyName) ? Mask : value;

    /// <summary>
    /// Маскирует адреса электронной почты и длинные последовательности цифр
    /// в произвольном тексте — на случай, если ПД попали в свободное поле.
    /// </summary>
    /// <param name="text">Текст для очистки.</param>
    /// <returns>Текст с замаскированными ПД.</returns>
    public static string? MaskFreeText(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = EmailRegex().Replace(text, Mask);
        result = LongDigitsRegex().Replace(result, Mask);
        return result;
    }

    [GeneratedRegex(@"[\w\.\-\+]+@[\w\-]+\.[\w\.\-]+", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"\b\d{9,}\b", RegexOptions.None, matchTimeoutMilliseconds: 200)]
    private static partial Regex LongDigitsRegex();
}
