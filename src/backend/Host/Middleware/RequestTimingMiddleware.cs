using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace ERPSystem.Host.Middleware;

/// <summary>
/// DEC-111: Lightweight APM — tracks request duration + status for each HTTP call.
/// Emits structured log event with timing data (compatible with Loki, Elasticsearch, Sentry).
/// No external APM SDK required — uses Serilog + Activity.
/// </summary>
public sealed class RequestTimingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestTimingMiddleware> _logger;

    public RequestTimingMiddleware(RequestDelegate next, ILogger<RequestTimingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var path = context.Request.Path.Value ?? "";
        var method = context.Request.Method;

        try
        {
            await _next(context);
        }
        finally
        {
            sw.Stop();
            var statusCode = context.Response.StatusCode;
            var elapsedMs = sw.ElapsedMilliseconds;

            // Slow request warning (> 1s)
            if (elapsedMs > 1000)
            {
                _logger.LogWarning("SLOW_REQUEST {Method} {Path} {StatusCode} {ElapsedMs}ms",
                    method, path, statusCode, elapsedMs);
            }
            // Error response (4xx, 5xx)
            else if (statusCode >= 400)
            {
                _logger.LogInformation("HTTP {Method} {Path} {StatusCode} {ElapsedMs}ms",
                    method, path, statusCode, elapsedMs);
            }
        }
    }
}
