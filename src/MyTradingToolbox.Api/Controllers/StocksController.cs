using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/stocks")]
public class StocksController : ControllerBase
{
    private readonly IStockCandleRepository _candleRepo;

    public StocksController(IStockCandleRepository candleRepo)
    {
        _candleRepo = candleRepo;
    }

    /// <summary>
    /// Returns daily stock price candles between from and to dates
    /// </summary>
    [HttpGet("candles/{symbol}")]
    public async Task<ActionResult<List<HistoricalStockCandle>>> GetStockCandles(
        string symbol,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var fromDate = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-1));
        var toDate = to ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var candles = await _candleRepo.GetCandlesAsync(symbol, fromDate, toDate, ct);
        return Ok(candles);
    }
}
