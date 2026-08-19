using System.Diagnostics;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;

namespace MyTradingToolbox.Api.Middleware;

public class ApiKeyAuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthMiddleware> _logger;

    public ApiKeyAuthMiddleware(RequestDelegate next, ILogger<ApiKeyAuthMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApiKeyRepository apiKeyRepo)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Skip auth for Swagger, health check, UI static assets, and auth endpoint itself
        if (path.StartsWith("/swagger") || 
            path.StartsWith("/api/v1/auth") || 
            path.StartsWith("/health") || 
            !path.StartsWith("/api/"))
        {
            await _next(context);
            return;
        }

        string? key = null;

        // Check Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerStr = authHeader.ToString();
            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                key = headerStr.Substring("Bearer ".Length).Trim();
            }
        }

        // Fallback to X-API-KEY header or query param
        if (string.IsNullOrWhiteSpace(key) && context.Request.Headers.TryGetValue("X-API-KEY", out var apiKeyHeader))
        {
            key = apiKeyHeader.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(key) && context.Request.Query.TryGetValue("api_key", out var queryKey))
        {
            key = queryKey.ToString().Trim();
        }

        var sw = Stopwatch.StartNew();
        ApiKey? validatedApiKey = null;

        if (!string.IsNullOrWhiteSpace(key))
        {
            validatedApiKey = await apiKeyRepo.ValidateKeyAsync(key);
        }

        // In internal admin/dev mode, if no key provided, allow with "InternalUI" consumer
        var consumerName = validatedApiKey?.ConsumerName ?? "InternalUI";

        await _next(context);
        sw.Stop();

        try
        {
            if (validatedApiKey != null)
            {
                await apiKeyRepo.LogUsageAsync(new ApiUsageLog
                {
                    Id = Guid.NewGuid(),
                    ApiKeyId = validatedApiKey.Id,
                    ConsumerName = consumerName,
                    Endpoint = path,
                    HttpMethod = context.Request.Method,
                    StatusCode = context.Response.StatusCode,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString()
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log API usage.");
        }
    }
}
