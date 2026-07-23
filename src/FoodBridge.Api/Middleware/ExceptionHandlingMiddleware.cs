using System.Net;
using System.Text.Json;
using FluentValidation;
using FoodBridge.Application.Common;
using FoodBridge.Domain.Exceptions;

namespace FoodBridge.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;
        var (statusCode, response) = MapException(exception, traceId);

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", traceId);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception mapped to {StatusCode}. TraceId: {TraceId}", statusCode, traceId);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;
        await context.Response.WriteAsync(JsonSerializer.Serialize(response, SerializerOptions));
    }

    private static (HttpStatusCode StatusCode, ApiResponse<object?> Response) MapException(Exception exception, string traceId) =>
        exception switch
        {
            NotFoundException notFound => (HttpStatusCode.NotFound, ApiResponse<object?>.Fail(notFound.Message, traceId: traceId)),
            ValidationException validation => (HttpStatusCode.BadRequest, ApiResponse<object?>.Fail(
                "One or more validation errors occurred.",
                validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}").ToList(),
                traceId)),
            BusinessRuleException businessRule => (HttpStatusCode.UnprocessableEntity, ApiResponse<object?>.Fail(businessRule.Message, traceId: traceId)),
            ConflictException conflict => (HttpStatusCode.Conflict, ApiResponse<object?>.Fail(conflict.Message, traceId: traceId)),
            RateLimitExceededException rateLimit => ((HttpStatusCode)429, ApiResponse<object?>.Fail(rateLimit.Message, traceId: traceId)),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, ApiResponse<object?>.Fail("You are not authorized to perform this action.", traceId: traceId)),
            _ => (HttpStatusCode.InternalServerError, ApiResponse<object?>.Fail("An unexpected error occurred. Please try again later.", traceId: traceId)),
        };
}
