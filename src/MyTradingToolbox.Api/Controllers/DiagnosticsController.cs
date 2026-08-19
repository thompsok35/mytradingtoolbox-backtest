using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Services.Diagnostics;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    private readonly ISystemDiagnosticsService _diagnostics;

    public DiagnosticsController(ISystemDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    /// <summary>
    /// Comprehensive system health matrix (DB ping, memory, uptime, Tradier status)
    /// </summary>
    [HttpGet("system-health")]
    public async Task<ActionResult<SystemHealthDto>> GetSystemHealth(CancellationToken ct)
    {
        var health = await _diagnostics.GetSystemHealthAsync(ct);
        return Ok(health);
    }

    /// <summary>
    /// Live test of Tradier API connectivity and latency
    /// </summary>
    [HttpPost("test-tradier")]
    public async Task<ActionResult<TradierHealthDto>> TestTradier(CancellationToken ct)
    {
        var result = await _diagnostics.TestTradierConnectivityAsync(ct);
        return Ok(result);
    }

    /// <summary>
    /// Returns live buffered system logs with optional severity filter
    /// </summary>
    [HttpGet("logs")]
    public ActionResult<List<SystemLogDto>> GetLogs([FromQuery] string? level = null, [FromQuery] int limit = 100)
    {
        var logs = _diagnostics.GetRecentLogs(level, limit);
        return Ok(logs);
    }
}
