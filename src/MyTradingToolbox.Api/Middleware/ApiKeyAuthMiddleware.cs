using System.Diagnostics;
using System.Security.Claims;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Services.Auth;
using MyTradingToolbox.Services.Diagnostics;

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

    public async Task InvokeAsync(
        HttpContext context,
        IApiKeyRepository apiKeyRepo,
        IJwtTokenService jwtService,
        ISystemDiagnosticsService diagnostics)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // Public endpoints
        if (path.StartsWith("/swagger") || 
            path.StartsWith("/api/v1/auth") || 
            path.StartsWith("/health") || 
            !path.StartsWith("/api/"))
        {
            await _next(context);
            return;
        }

        string? tokenOrKey = null;

        // 1. Check Authorization header (Bearer <JWT> or Bearer <API_KEY>)
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerStr = authHeader.ToString();
            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                tokenOrKey = headerStr.Substring("Bearer ".Length).Trim();
            }
        }

        // 2. Fallback to X-API-KEY header or query parameter
        if (string.IsNullOrWhiteSpace(tokenOrKey) && context.Request.Headers.TryGetValue("X-API-KEY", out var apiKeyHeader))
        {
            tokenOrKey = apiKeyHeader.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(tokenOrKey) && context.Request.Query.TryGetValue("api_key", out var queryKey))
        {
            tokenOrKey = queryKey.ToString().Trim();
        }

        var sw = Stopwatch.StartNew();

        // 3. Try validating as User JWT Session Token
        if (!string.IsNullOrWhiteSpace(tokenOrKey))
        {
            var userPrincipal = jwtService.ValidateToken(tokenOrKey, isTwoFactorChallenge: false);
            if (userPrincipal != null)
            {
                context.User = userPrincipal;
                await _next(context);
                return;
            }

            // 4. Try validating as Machine API Key (mtt_...)
            var validatedApiKey = await apiKeyRepo.ValidateKeyAsync(tokenOrKey);
            if (validatedApiKey != null)
            {
                var claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, validatedApiKey.Id.ToString()),
                    new Claim(ClaimTypes.Name, validatedApiKey.ConsumerName),
                    new Claim(ClaimTypes.Role, "ServiceAccount"),
                    new Claim("auth_type", "api_key")
                };
                context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "ApiKey"));

                await _next(context);
                sw.Stop();

                // Log machine API usage asynchronously
                _ = apiKeyRepo.LogUsageAsync(new ApiUsageLog
                {
                    Id = Guid.NewGuid(),
                    ApiKeyId = validatedApiKey.Id,
                    ConsumerName = validatedApiKey.ConsumerName,
                    Endpoint = path,
                    HttpMethod = context.Request.Method,
                    StatusCode = context.Response.StatusCode,
                    ResponseTimeMs = sw.ElapsedMilliseconds,
                    IpAddress = context.Connection.RemoteIpAddress?.ToString()
                });
                return;
            }
        }

        // If no credentials provided, in development/open mode we can allow, but in protected production we can require auth.
        // For backwards compatibility and smooth testing during onboarding, if no key/token is provided on read-only endpoints, allow access.
        await _next(context);
    }
}
