namespace GarageFlow.Api.Middleware;

/// <summary>
/// Gives every request an ID that appears in every log line it causes, in the response header, and in error bodies.
/// "My request failed, ID 3f9..." lets you find exactly that request's logs. A caller (or gateway) may supply its own
/// ID in the X-Correlation-ID header so one ID can follow a request through several systems.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";
    public const string ItemKey = "CorrelationId";

    public async Task InvokeAsync(HttpContext context)
    {
        var id = context.Request.Headers[HeaderName].FirstOrDefault();
        if (!IsAcceptable(id))
            id = Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        // Everything logged while this scope is open carries the ID (the console formatter prints scopes).
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = id! }))
        {
            await next(context);
        }
    }

    public static string? Get(HttpContext context) => context.Items[ItemKey] as string;

    // The value is copied into logs and headers, so accept only short, plain text (no log injection via new lines).
    private static bool IsAcceptable(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 64 && value.All(c => char.IsLetterOrDigit(c) || c is '-' or '_');
}
