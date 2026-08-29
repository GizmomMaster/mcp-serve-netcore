using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Аутентификация сервисных аккаунтов по API-ключу (FR-42).
/// Предназначена для harness, который не умеет в OAuth2.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<ApiKeyAuthenticationOptions> optionsMonitor,
    ILoggerFactory loggerFactory,
    UrlEncoder encoder)
    : AuthenticationHandler<ApiKeyAuthenticationOptions>(optionsMonitor, loggerFactory, encoder)
{
    /// <summary>Имя схемы аутентификации.</summary>
    public const string SchemeName = "ApiKey";

    /// <summary>Заголовок, в котором передаётся ключ.</summary>
    public const string HeaderName = "X-Api-Key";

    /// <inheritdoc />
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var headerValues))
        {
            // Не ошибка: запрос может аутентифицироваться другой схемой (JWT).
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var presentedKey = headerValues.FirstOrDefault();

        if (string.IsNullOrWhiteSpace(presentedKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Пустой API-ключ."));
        }

        var client = FindClient(presentedKey);

        if (client is null)
        {
            return Task.FromResult(AuthenticateResult.Fail("Неизвестный API-ключ."));
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, client.ClientId),
            new(ClaimTypes.Name, client.ClientId),
            new(HttpSubjectAccessor.SubjectKindClaim, "service"),
        };

        claims.AddRange(client.Groups.Select(
            group => new Claim(HttpSubjectAccessor.GroupsClaim, group)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);

        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(principal, SchemeName)));
    }

    /// <summary>
    /// Ищет клиента по ключу, сравнивая значения за постоянное время,
    /// чтобы ключ нельзя было подобрать по времени ответа.
    /// </summary>
    private ApiKeyClient? FindClient(string presentedKey)
    {
        var presentedBytes = Encoding.UTF8.GetBytes(presentedKey);
        ApiKeyClient? matched = null;

        foreach (var client in Options.Clients)
        {
            var candidateBytes = Encoding.UTF8.GetBytes(client.Key);

            if (CryptographicOperations.FixedTimeEquals(presentedBytes, candidateBytes))
            {
                matched = client;
            }
        }

        return matched;
    }
}

/// <summary>
/// Настройки схемы аутентификации по API-ключу (FR-42).
/// </summary>
public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>Секция конфигурации.</summary>
    public const string SectionName = "ApiKeyAuth";

    /// <summary>
    /// Зарегистрированные сервисные клиенты.
    /// Сами ключи задаются только через Vault или переменные окружения (NFR-35).
    /// </summary>
    public IList<ApiKeyClient> Clients { get; init; } = [];
}

/// <summary>
/// Сервисный клиент, аутентифицируемый по ключу (раздел 8.1).
/// </summary>
public sealed class ApiKeyClient
{
    /// <summary>Идентификатор сервисного аккаунта.</summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Секретный ключ.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Группы доступа, определяющие видимые разделы wiki (FR-44).</summary>
    public IList<string> Groups { get; init; } = [];
}
