using System.Net;
using System.Text.Json;
using FluentValidation;
using IhsanAI.Application.Common.Models;
using IhsanAI.Application.Common.Exceptions;
using IhsanAI.Domain.Exceptions;

namespace IhsanAI.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IWebHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IWebHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var errorId = Guid.NewGuid().ToString("N")[..8]; // Kısa 8 karakterlik ID
            _logger.LogError(ex, "Error {ErrorId}: An unhandled exception occurred", errorId);
            await HandleExceptionAsync(context, ex, errorId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string errorId)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, errors) = exception switch
        {
            ValidationException validationException => (
                HttpStatusCode.BadRequest,
                validationException.Errors.Select(e => new Error(e.PropertyName, e.ErrorMessage))
            ),
            DomainException domainException => (
                HttpStatusCode.BadRequest,
                new[] { new Error("DomainError", domainException.Message) }
            ),
            ForbiddenAccessException forbiddenException => (
                HttpStatusCode.Forbidden,
                new[] { new Error("ForbiddenAccess", forbiddenException.Message) }
            ),
            UnauthorizedAccessException => (
                HttpStatusCode.Unauthorized,
                new[] { Error.Unauthorized }
            ),
            KeyNotFoundException => (
                HttpStatusCode.NotFound,
                new[] { Error.NotFound }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new[] { new Error("ServerError",
                    _environment.IsDevelopment()
                        ? exception.Message
                        : $"Bir hata oluştu. Hata kodu: {errorId}") }
            )
        };

        context.Response.StatusCode = (int)statusCode;

        var result = Result.Failure(errors);
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
