using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Procurement.Api;

/// <summary>An expected failure that maps to a ProblemDetails response with a readable detail.</summary>
public class ApiProblemException(int statusCode, string title, string detail) : Exception(detail)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;
}

/// <summary>Invalid input: 400.</summary>
public sealed class ValidationProblemException(string detail)
    : ApiProblemException(StatusCodes.Status400BadRequest, "Invalid request", detail);

/// <summary>Unknown id: 404.</summary>
public sealed class NotFoundProblemException(string detail)
    : ApiProblemException(StatusCodes.Status404NotFound, "Not found", detail);

/// <summary>Duplicate of an existing record: 409.</summary>
public sealed class ConflictProblemException(string detail)
    : ApiProblemException(StatusCodes.Status409Conflict, "Conflict", detail);

/// <summary>Valid input that breaks a business rule: 422.</summary>
public sealed class RuleViolationProblemException(string detail)
    : ApiProblemException(StatusCodes.Status422UnprocessableEntity, "Rule violation", detail);

/// <summary>
/// Turns <see cref="ApiProblemException"/> and request-binding failures into ProblemDetails.
/// Anything else falls through to the default 500 handling.
/// </summary>
public sealed class ProblemExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ApiProblemException p => (p.StatusCode, p.Title, p.Message),
            BadHttpRequestException b => (b.StatusCode, "Invalid request", Describe(b)),
            _ => (0, "", ""),
        };

        if (status == 0)
        {
            return false;
        }

        httpContext.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }

    private static string Describe(BadHttpRequestException exception) => exception.InnerException switch
    {
        JsonException { Path: { Length: > 0 } path } =>
            $"The JSON body has an invalid value at {path}. Check that field's type and format.",
        JsonException => "The request body is not valid JSON.",
        _ when exception.Message.Contains("body", StringComparison.OrdinalIgnoreCase) =>
            "A JSON request body is required.",
        _ => $"The request could not be read: {exception.Message}",
    };
}
