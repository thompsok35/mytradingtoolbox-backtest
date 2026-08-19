using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Services.Integrity;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/market")]
public class MarketController : ControllerBase
{
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IDataIntegrityService _integrityService;

    public MarketController(IWatchlistRepository watchlistRepo, IDataIntegrityService integrityService)
    {
        _watchlistRepo = watchlistRepo;
        _integrityService = integrityService;
    }

    /// <summary>
    /// Returns available date ranges and data health status for a ticker
    /// </summary>
    [HttpGet("coverage/{symbol}")]
    public async Task<ActionResult<MarketCoverageDto>> GetCoverage(string symbol, CancellationToken ct)
    {
        var coverage = await _integrityService.GetCoverageAsync(symbol, ct);
        return Ok(coverage);
    }

    /// <summary>
    /// Returns all tracked watchlist symbols and their metadata
    /// </summary>
    [HttpGet("watchlist")]
    public async Task<ActionResult<List<WatchlistSymbol>>> GetWatchlist(CancellationToken ct)
    {
        var list = await _watchlistRepo.GetAllAsync(ct);
        return Ok(list);
    }

    /// <summary>
    /// Adds or updates a symbol in the watchlist
    /// </summary>
    [HttpPost("watchlist")]
    public async Task<ActionResult<WatchlistSymbol>> AddSymbol([FromBody] WatchlistSymbol request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return BadRequest("Symbol is required.");

        var result = await _watchlistRepo.AddOrUpdateAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Toggles active automated daily harvesting for a symbol
    /// </summary>
    [HttpPut("watchlist/{symbol}/toggle")]
    public async Task<ActionResult> ToggleHarvesting(string symbol, [FromQuery] bool active, CancellationToken ct)
    {
        var success = await _watchlistRepo.ToggleHarvestingAsync(symbol, active, ct);
        if (!success) return NotFound();
        return Ok(new { symbol, isActive = active });
    }

    /// <summary>
    /// Deletes a symbol from the watchlist
    /// </summary>
    [HttpDelete("watchlist/{symbol}")]
    public async Task<ActionResult> DeleteSymbol(string symbol, CancellationToken ct)
    {
        var success = await _watchlistRepo.DeleteAsync(symbol, ct);
        if (!success) return NotFound();
        return NoContent();
    }
}
