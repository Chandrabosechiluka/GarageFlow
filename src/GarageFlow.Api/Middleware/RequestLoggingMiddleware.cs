using System.Diagnostics;

namespace GarageFlow.Api.Middleware;

/// <summary>
/// One log line per request: method, path, final status and how long it took. It sits OUTSIDE the exception handler,
/// so a request that crashed is logged with the 500 the caller actually received.
/// Query strings are not logged (they can hold personal data).
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var timer = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            timer.Stop();
            var status = context.Response.StatusCode;
            var level = status >= 500 ? LogLevel.Error
                : status >= 400 ? LogLevel.Warning
                : context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug
                : LogLevel.Information;

            logger.Log(level, "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs} ms",
                context.Request.Method, context.Request.Path.Value, status, timer.ElapsedMilliseconds);
        }
    }
}
