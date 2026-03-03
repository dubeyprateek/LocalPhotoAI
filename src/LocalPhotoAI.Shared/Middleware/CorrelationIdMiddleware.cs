using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace LocalPhotoAI.Shared.Middleware;

/// <summary>
/// Adds a correlation ID to each request for end-to-end tracing.
/// </summary>
public class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString();
        }

        context.Items["CorrelationId"] = correlationId.ToString();
        context.Response.Headers[HeaderName] = correlationId.ToString();

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId.ToString()! }))
        {
            await _next(context);
        }
    }
}
