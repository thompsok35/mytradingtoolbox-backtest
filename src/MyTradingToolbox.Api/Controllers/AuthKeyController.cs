using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthKeyController : ControllerBase
{
    private readonly IApiKeyRepository _apiKeyRepo;

    public AuthKeyController(IApiKeyRepository apiKeyRepo)
    {
        _apiKeyRepo = apiKeyRepo;
    }

    /// <summary>
    /// Lists all consumer application API keys
    /// </summary>
    [HttpGet("keys")]
    public async Task<ActionResult<List<ApiKey>>> GetAllKeys(CancellationToken ct)
    {
        var keys = await _apiKeyRepo.GetAllKeysAsync(ct);
        return Ok(keys);
    }

    public record CreateKeyRequest(string ConsumerName, int RateLimitPerMinute = 120, DateTime? ExpiresAt = null);

    /// <summary>
    /// Generates a new Bearer API Key for a consumer app (itmCCbot, Market Insights)
    /// </summary>
    [HttpPost("keys")]
    public async Task<ActionResult<ApiKey>> CreateKey([FromBody] CreateKeyRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ConsumerName))
            return BadRequest("ConsumerName is required.");

        var key = await _apiKeyRepo.CreateKeyAsync(request.ConsumerName, request.RateLimitPerMinute, request.ExpiresAt, ct);
        return Ok(key);
    }

    /// <summary>
    /// Revokes an API Key
    /// </summary>
    [HttpDelete("keys/{id}")]
    public async Task<ActionResult> RevokeKey(Guid id, CancellationToken ct)
    {
        var success = await _apiKeyRepo.RevokeKeyAsync(id, ct);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Returns recent API usage logs
    /// </summary>
    [HttpGet("logs")]
    public async Task<ActionResult<List<ApiUsageLog>>> GetLogs([FromQuery] int count = 100, CancellationToken ct = default)
    {
        var logs = await _apiKeyRepo.GetRecentLogsAsync(count, ct);
        return Ok(logs);
    }
}
