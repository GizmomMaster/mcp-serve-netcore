using CentralWikiMcp.Server.Tools;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace CentralWikiMcp.Server.Endpoints;

/// <summary>
/// Единая обработка ошибок (FR-05, FR-06, FR-45).
/// </summary>
public static class ErrorHandling
{
    /// <summary>
    /// Настраивает конвейер обработки необработанных исключений.
    /// </summary>
    /// <param name="app">Конвейер обработчика ошибок.</param>
    public static void Handler(IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.Run(async context =>
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            var exception = feature?.Error;

            var (statusCode, error, message) = Map(exception);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsJsonAsync(new
            {
                type = $"https://httpstatuses.io/{statusCode}",
                title = error,
                status = statusCode,
                detail = message,
                traceId = context.TraceIdentifier,
            }).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Сопоставляет исключение с ответом.
    /// Внутренние детали наружу не выносятся: сообщение отдаётся только
    /// для ожидаемых доменных ошибок.
    /// </summary>
    private static (int StatusCode, string Error, string Message) Map(Exception? exception) =>
        exception switch
        {
            // FR-45: отсутствие прав — 403, а не пустой результат.
            WikiAccessDeniedException denied =>
                (StatusCodes.Status403Forbidden, "forbidden", denied.Message),

            Exception notFound when notFound is WikiPageNotFoundException or WikiSectionNotFoundException =>
                (StatusCodes.Status404NotFound, "not_found", notFound.Message),

            ValidationException validation =>
                (StatusCodes.Status400BadRequest, "validation_failed", validation.Message),

            // FR-63: превышение таймаута поиска.
            OperationCanceledException =>
                (StatusCodes.Status504GatewayTimeout, "timeout", "Запрос превысил отведённое время."),

            _ => (
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Внутренняя ошибка сервиса."),
        };
}
