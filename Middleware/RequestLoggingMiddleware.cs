namespace ZaffreMeld.Web.Middleware;

/// <summary>
/// Middleware that logs each request — mirrors the Java bslog() pattern.
/// </summary>
public class ZaffreMeldRequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ZaffreMeldRequestLoggingMiddleware> _logger;

    public ZaffreMeldRequestLoggingMiddleware(RequestDelegate next, ILogger<ZaffreMeldRequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        try
        {
            await _next(context);
        }
        finally
        {
            var elapsed = (DateTime.UtcNow - start).TotalMilliseconds;
            var user = context.User?.Identity?.Name ?? "anonymous";
            _logger.LogDebug("ZaffreMeld [{Method}] {Path} -> {Status} ({Elapsed:F0}ms) user={User}",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                elapsed,
                user);
        }
    }
}

public static class ZaffreMeldRequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseZaffreMeldRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<ZaffreMeldRequestLoggingMiddleware>();
}
