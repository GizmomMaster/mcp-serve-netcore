using Xunit;
using Abdt.Infrastructure.Logging.AspNetCore;

namespace CentralWikiMcp.UnitTests;

/// <summary>
/// Проверки маскирования чувствительных данных в логах (FR-52, NFR-38).
/// </summary>
public sealed class LogMaskingTests
{
    [Theory]
    [InlineData("token")]
    [InlineData("Authorization")]
    [InlineData("PASSWORD")]
    [InlineData("X-Api-Key")]
    [InlineData("client_secret")]
    [InlineData("ConnectionString")]
    [InlineData("refresh_token")]
    public void Sensitive_property_names_are_recognised(string propertyName)
    {
        Assert.True(LogMasking.IsPrivate(propertyName));
        Assert.Equal(LogMasking.Mask, LogMasking.MaskIfPrivate(propertyName, "секрет"));
    }

    [Theory]
    [InlineData("query")]
    [InlineData("pageId")]
    [InlineData("namespace")]
    [InlineData("top_k")]
    public void Regular_property_names_pass_through(string propertyName)
    {
        Assert.False(LogMasking.IsPrivate(propertyName));
        Assert.Equal("значение", LogMasking.MaskIfPrivate(propertyName, "значение"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_property_name_is_not_private(string? propertyName) =>
        Assert.False(LogMasking.IsPrivate(propertyName));

    [Fact]
    public void Email_in_free_text_is_masked()
    {
        var masked = LogMasking.MaskFreeText("Напишите на ivan.petrov@example.com сегодня");

        Assert.DoesNotContain("ivan.petrov@example.com", masked, StringComparison.Ordinal);
        Assert.Contains(LogMasking.Mask, masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_digit_sequences_are_masked()
    {
        // Длинные цифровые последовательности — это телефоны, счета и документы.
        var masked = LogMasking.MaskFreeText("Договор 1234567890123 подписан");

        Assert.DoesNotContain("1234567890123", masked, StringComparison.Ordinal);
    }

    [Fact]
    public void Short_numbers_are_left_intact()
    {
        var masked = LogMasking.MaskFreeText("Вернуть 10 результатов");

        Assert.Equal("Вернуть 10 результатов", masked);
    }

    [Fact]
    public void Null_free_text_is_returned_as_is() =>
        Assert.Null(LogMasking.MaskFreeText(null));
}
