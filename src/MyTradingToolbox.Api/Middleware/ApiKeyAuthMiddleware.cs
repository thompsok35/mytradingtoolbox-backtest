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
        // 1. Immediately handle CORS preflight OPTIONS requests
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            return;
        }

        var path = context.Request.Path.Value ?? string.Empty;

        // 2. Explicitly allowed public endpoints
        bool isPublicEndpoint = path.StartsWith("/swagger") || 
                                path.Equals("/health", StringComparison.OrdinalIgnoreCase) || 
                                path.Equals("/api/v1/auth/config", StringComparison.OrdinalIgnoreCase) || 
                                path.Equals("/api/v1/auth/google", StringComparison.OrdinalIgnoreCase) || 
                                (path.Equals("/api/v1/auth/2fa/verify", StringComparison.OrdinalIgnoreCase) && context.Request.Method == "POST") ||
                                !path.StartsWith("/api/");

        if (isPublicEndpoint)
        {
            await _next(context);
            return;
        }

        string? tokenOrKey = null;

        // 3. Extract Authorization header (Bearer <JWT> or Bearer <API_KEY>)
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerStr = authHeader.ToString();
            if (headerStr.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                tokenOrKey = headerStr.Substring("Bearer ".Length).Trim();
            }
        }

        // 4. Fallback to X-API-KEY header or query parameter
        if (string.IsNullOrWhiteSpace(tokenOrKey) && context.Request.Headers.TryGetValue("X-API-KEY", out var apiKeyHeader))
        {
            tokenOrKey = apiKeyHeader.ToString().Trim();
        }

        if (string.IsNullOrWhiteSpace(tokenOrKey) && context.Request.Query.TryGetValue("api_key", out var queryKey))
        {
            tokenOrKey = queryKey.ToString().Trim();
        }

        var sw = Stopwatch.StartNew();

        // 5. Try validating as User JWT Session Token
        if (!string.IsNullOrWhiteSpace(tokenOrKey))
        {
            var userPrincipal = jwtService.ValidateToken(tokenOrKey, isTwoFactorChallenge: false);
            if (userPrincipal != null)
            {
                context.User = userPrincipal;
                await _next(context);
                return;
            }

            // 6. Try validating as Machine API Key (mtt_...)
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

        // 7. Reject unauthenticated requests to protected endpoints
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync("{\"error\":\"Unauthorized\",\"message\":\"Authentication required. Please sign in with Google or provide a valid API Key.\"}");
    }
}
