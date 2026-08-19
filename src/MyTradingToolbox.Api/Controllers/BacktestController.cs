using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Services.Backtest;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/backtest")]
public class BacktestController : ControllerBase
{
    private readonly IBacktestEngine _backtestEngine;

    public BacktestController(IBacktestEngine backtestEngine)
    {
        _backtestEngine = backtestEngine;
    }

    /// <summary>
    /// Server-side execution of standardized ITM Covered Call or custom options strategy
    /// </summary>
    [HttpPost("execute")]
    public async Task<ActionResult<BacktestResult>> ExecuteBacktest([FromBody] BacktestRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return BadRequest("Symbol is required.");

        if (request.StartDate == default)
            request.StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));

        if (request.EndDate == default)
            request.EndDate = DateOnly.FromDateTime(DateTime.UtcNow);

        if (request.StartDate >= request.EndDate)
            return BadRequest("StartDate must be prior to EndDate.");

        var result = await _backtestEngine.ExecuteBacktestAsync(request, ct);
        return Ok(result);
    }
}
