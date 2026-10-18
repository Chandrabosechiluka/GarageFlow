using GarageFlow.Application.Common;
using GarageFlow.Domain.Common;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GarageFlow.Api.Errors;

/// <summary>
/// The one place that turns exceptions into HTTP responses, so controllers never need try/catch.
/// Every error comes back in the standard "problem details" JSON shape.
/// </summary>
public class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title, string? Detail) outcome = exception switch
        {
            AuthenticationFailedException => (StatusCodes.Status401Unauthorized, "Authentication failed", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            DomainException => (StatusCodes.Status400BadRequest, "Business rule violated", exception.Message),

            // Optimistic concurrency: the row version changed between loading and saving (someone else edited it first).
            DbUpdateConcurrencyException => (StatusCodes.Status409Conflict, "Conflict",
                "This record was changed by someone else. Reload it and try again."),

            // The database's own unique index refused the write (2601 / 2627): somebody saved the same thing a moment ago.
            DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict, "Conflict",
                "That conflicts with data that was just saved by someone else. Please try again."),

            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred", null)
        };

        if (outcome.Status == StatusCodes.Status500InternalServerError)
            logger.LogError(exception, "Unhandled exception");

        var problem = new ProblemDetails
        {
            Status = outcome.Status,
            Title = outcome.Title,
            // Never leak internal details (SQL messages, stack traces) to the caller.
            Detail = outcome.Detail
        };

        httpContext.Response.StatusCode = outcome.Status;
        await httpContext.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json", cancellationToken);
        return true;
    }
}
