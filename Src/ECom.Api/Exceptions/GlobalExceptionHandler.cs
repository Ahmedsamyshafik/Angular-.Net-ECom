using System.Diagnostics;
using System.Text.Json;
using Ecom.Core.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECom.Api.Exceptions;

/// <summary>
/// The single place that converts exceptions into HTTP responses.
/// Is registered via AddExceptionHandler and activated by UseExceptionHandler.
/// Known/expected failures are mapped to their HTTP status (see mapping switch),
/// unexpected exceptions become a generic 500 and are logged with full details.
/// Internal details (stack traces, EF/SQL messages) are never sent to the client.
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        // Order matters: DbUpdateConcurrencyException must be matched explicitly;
        // it derives from DbUpdateException which is deliberately left as 500
        // because a general DB failure is an unexpected infrastructure problem.
        var statusCode = exception switch
        {
            NotFoundException => StatusCodes.Status404NotFound,
            BadRequestException => StatusCodes.Status400BadRequest,
            ConflictException => StatusCodes.Status409Conflict,
            DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
            BadHttpRequestException => StatusCodes.Status400BadRequest,
            ArgumentOutOfRangeException => StatusCodes.Status400BadRequest,
            _ => StatusCodes.Status500InternalServerError
        };

        if (statusCode == StatusCodes.Status500InternalServerError)
        {
            _logger.LogError(exception,
                "Unhandled exception for request {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception,
                "Expected failure for request {Method} {Path}, mapped to status {StatusCode}",
                httpContext.Request.Method, httpContext.Request.Path, statusCode);
        }

        // Detail text: only our own business messages are forwarded verbatim.
        // Framework/EF messages are replaced with a safe, generic wording.
        var detail = exception switch
        {
            NotFoundException or BadRequestException or ConflictException or ArgumentOutOfRangeException => exception.Message,
            DbUpdateConcurrencyException => "The record was modified by another user. Please reload and try again.",
            BadHttpRequestException => "The request is malformed and could not be understood.",
            _ => "An unexpected error occurred. Please try again later."
        };

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = GetTitleFor(statusCode),
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsync(JsonSerializer.Serialize(problemDetails), cancellationToken);
        return true;
    }

    private static string GetTitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Bad Request",
        StatusCodes.Status401Unauthorized => "Unauthorized",
        StatusCodes.Status403Forbidden => "Forbidden",
        StatusCodes.Status404NotFound => "Not Found",
        StatusCodes.Status409Conflict => "Conflict",
        StatusCodes.Status500InternalServerError => "Internal Server Error",
        _ => "Error"
    };
}