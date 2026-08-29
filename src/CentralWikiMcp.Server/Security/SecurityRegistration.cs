using CentralWikiMcp.Server.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace CentralWikiMcp.Server.Security;

/// <summary>
/// Регистрация аутентификации и авторизации (FR-40…FR-45).
/// </summary>
public static class SecurityRegistration
{
    /// <summary>Секция конфигурации с параметрами JWT.</summary>
    public const string JwtSectionName = "Jwt";

    /// <summary>Группы администраторов по умолчанию, если конфигурация их не задаёт.</summary>
    private static readonly string[] DefaultAdminGroups = ["wiki-admins"];

    /// <summary>Группы аудиторов по умолчанию, если конфигурация их не задаёт.</summary>
    private static readonly string[] DefaultAuditorGroups = ["wiki-auditors"];

    /// <summary>
    /// Подключает аутентификацию по JWT и API-ключу (FR-40…FR-43).
    /// Схема выбирается по наличию заголовка с ключом, поэтому harness и живой
    /// пользователь ходят в один и тот же эндпоинт.
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <param name="environment">Окружение приложения.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddWikiAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = AuthenticationSchemes.Composite;
                options.DefaultChallengeScheme = AuthenticationSchemes.Composite;
            })
            .AddPolicyScheme(
                AuthenticationSchemes.Composite,
                AuthenticationSchemes.Composite,
                options => options.ForwardDefaultSelector = context =>
                    context.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
                        ? ApiKeyAuthenticationHandler.SchemeName
                        : JwtBearerDefaults.AuthenticationScheme)
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(
                ApiKeyAuthenticationHandler.SchemeName,
                options => configuration
                    .GetSection(ApiKeyAuthenticationOptions.SectionName)
                    .Bind(options))
            .AddJwtBearer(options =>
            {
                configuration.GetSection(JwtSectionName).Bind(options);

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration[$"{JwtSectionName}:Issuer"],
                    ValidAudience = configuration[$"{JwtSectionName}:Audience"],
                    ClockSkew = TimeSpan.FromMinutes(1),
                };

                // NFR-30: метаданные IdP тянем только по HTTPS.
                options.RequireHttpsMetadata = !environment.IsDevelopment();
            });

        return services;
    }

    /// <summary>
    /// Подключает авторизацию: аутентификация обязательна для всех эндпоинтов,
    /// административные и аудиторские требуют членства в группах (FR-44, разделы 8.3, 8.4).
    /// </summary>
    /// <param name="services">Коллекция сервисов.</param>
    /// <param name="configuration">Конфигурация приложения.</param>
    /// <returns>Та же коллекция для цепочки вызовов.</returns>
    public static IServiceCollection AddWikiAuthorization(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddAuthorizationBuilder()

            // NFR-31: эндпоинт без явной политики всё равно требует аутентификации.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(
                AdminEndpoints.AdminPolicy,
                policy => policy.RequireAuthenticatedUser().RequireClaim(
                    HttpSubjectAccessor.GroupsClaim,
                    GetGroups(configuration, "AdminGroups", DefaultAdminGroups)))
            .AddPolicy(
                AdminEndpoints.AuditorPolicy,
                policy => policy.RequireAuthenticatedUser().RequireClaim(
                    HttpSubjectAccessor.GroupsClaim,
                    GetGroups(configuration, "AuditorGroups", DefaultAuditorGroups)));

        return services;
    }

    private static string[] GetGroups(
        IConfiguration configuration,
        string key,
        string[] fallback) =>
        configuration.GetSection($"AccessPolicy:{key}").Get<string[]>() ?? fallback;
}
