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

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    private static async Task HandleExceptionAsync(HttpContext context, Exception exception)
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
                new[] { new Error("ServerError", exception.Message + " | " + exception.InnerException?.Message) }
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
