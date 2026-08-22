using Microsoft.AspNetCore.Mvc;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;

namespace MyTradingToolbox.Api.Controllers;

[ApiController]
[Route("api/v1/options")]
public class OptionsController : ControllerBase
{
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly IStockCandleRepository _candleRepo;

    public OptionsController(IOptionSnapshotRepository optionRepo, IStockCandleRepository candleRepo)
    {
        _optionRepo = optionRepo;
        _candleRepo = candleRepo;
    }

    /// <summary>
    /// Returns option chain for a given date with DTE, Strike, Delta, and Greek filters
    /// </summary>
    [HttpGet("chain/{symbol}")]
    public async Task<ActionResult<OptionChainResponseDto>> GetOptionChain(
        string symbol,
        [FromQuery] DateOnly? date,
        [FromQuery] int? minDte,
        [FromQuery] int? maxDte,
        [FromQuery] decimal? minStrike,
        [FromQuery] decimal? maxStrike,
        [FromQuery] OptionSide? side,
        [FromQuery] decimal? minDelta,
        [FromQuery] decimal? maxDelta,
        [FromQuery] decimal? minIv,
        [FromQuery] decimal? maxIv,
        [FromQuery] DateOnly? expirationDate,
        CancellationToken ct)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        
        // If date not provided, use latest available date for this symbol
        if (!date.HasValue)
        {
            var dates = await _optionRepo.GetAvailableDatesAsync(symbol, ct);
            if (dates.Count > 0)
            {
                date = dates.Last();
            }
            else
            {
                date = DateOnly.FromDateTime(DateTime.UtcNow);
            }
        }

        var filter = new OptionChainFilter
        {
            Symbol = symbol,
            Date = date,
            MinDte = minDte,
            MaxDte = maxDte,
            MinStrike = minStrike,
            MaxStrike = maxStrike,
            Side = side,
            MinDelta = minDelta,
            MaxDelta = maxDelta,
            MinIV = minIv,
            MaxIV = maxIv,
            ExpirationDate = expirationDate
        };

        var snapshots = await _optionRepo.GetChainAsync(filter, ct);
        
        decimal underlyingPrice = 0m;
        if (snapshots.Count > 0 && snapshots.First().UnderlyingPrice > 0)
        {
            underlyingPrice = snapshots.First().UnderlyingPrice;
        }
        else
        {
            var candleList = await _candleRepo.GetCandlesAsync(symbol, date.Value, date.Value, ct);
            if (candleList.Count > 0)
            {
                underlyingPrice = candleList[0].Close;
            }
            else
            {
                var candle = await _candleRepo.GetLatestCandleAsync(symbol, ct);
                if (candle != null) underlyingPrice = candle.Close;
            }
        }

        var calls = snapshots
            .Where(s => s.Side == OptionSide.Call)
            .Select(s =>
            {
                var delta = s.Delta;
                var iv = s.ImpliedVolatility;
                var gamma = s.Gamma;
                var theta = s.Theta;
                var vega = s.Vega;
                decimal? probItm = null;

                if (underlyingPrice > 0)
                {
                    var optPrice = s.Mid > 0 ? s.Mid : (s.Last > 0 ? s.Last : Math.Max(0.01m, underlyingPrice - s.Strike));
                    var greeks = MyTradingToolbox.Core.Calculators.BlackScholesCalculator.ComputeGreeks(
                        underlyingPrice, s.Strike, s.DTE, s.Side, optPrice);
                    probItm = greeks.ProbabilityOfITM;

                    if (delta == null || delta == 0)
                    {
                        delta = greeks.Delta;
                        gamma = greeks.Gamma;
                        theta = greeks.Theta;
                        vega = greeks.Vega;
                        iv = greeks.IV;
                    }
                }

                return new OptionContractDto
                {
                    OptionSymbol = s.OptionSymbol,
                    ExpirationDate = s.ExpirationDate,
                    DTE = s.DTE,
                    Strike = s.Strike,
                    Side = s.Side,
                    Bid = s.Bid,
                    Ask = s.Ask,
                    Mid = s.Mid,
                    Last = s.Last,
                    Delta = delta,
                    Gamma = gamma,
                    Theta = theta,
                    Vega = vega,
                    Rho = s.Rho,
                    ImpliedVolatility = iv,
                    ProbabilityOfITM = probItm,
                    Volume = s.Volume,
                    OpenInterest = s.OpenInterest,
                    DataSource = s.DataSource
                };
            })
            .ToList();

        var puts = snapshots
            .Where(s => s.Side == OptionSide.Put)
            .Select(s =>
            {
                var delta = s.Delta;
                var iv = s.ImpliedVolatility;
                var gamma = s.Gamma;
                var theta = s.Theta;
                var vega = s.Vega;
                decimal? probItm = null;

                if (underlyingPrice > 0)
                {
                    var optPrice = s.Mid > 0 ? s.Mid : (s.Last > 0 ? s.Last : Math.Max(0.01m, s.Strike - underlyingPrice));
                    var greeks = MyTradingToolbox.Core.Calculators.BlackScholesCalculator.ComputeGreeks(
                        underlyingPrice, s.Strike, s.DTE, s.Side, optPrice);
                    probItm = greeks.ProbabilityOfITM;

                    if (delta == null || delta == 0)
                    {
                        delta = greeks.Delta;
                        gamma = greeks.Gamma;
                        theta = greeks.Theta;
                        vega = greeks.Vega;
                        iv = greeks.IV;
                    }
                }

                return new OptionContractDto
                {
                    OptionSymbol = s.OptionSymbol,
                    ExpirationDate = s.ExpirationDate,
                    DTE = s.DTE,
                    Strike = s.Strike,
                    Side = s.Side,
                    Bid = s.Bid,
                    Ask = s.Ask,
                    Mid = s.Mid,
                    Last = s.Last,
                    Delta = delta,
                    Gamma = gamma,
                    Theta = theta,
                    Vega = vega,
                    Rho = s.Rho,
                    ImpliedVolatility = iv,
                    ProbabilityOfITM = probItm,
                    Volume = s.Volume,
                    OpenInterest = s.OpenInterest,
                    DataSource = s.DataSource
                };
            })
            .ToList();

        return Ok(new OptionChainResponseDto
        {
            Symbol = symbol,
            SnapshotDate = date.Value,
            UnderlyingPrice = underlyingPrice,
            Calls = calls,
            Puts = puts
        });
    }

    /// <summary>
    /// Returns all available snapshot dates for a symbol
    /// </summary>
    [HttpGet("dates/{symbol}")]
    public async Task<ActionResult<List<DateOnly>>> GetAvailableDates(string symbol, CancellationToken ct)
    {
        var dates = await _optionRepo.GetAvailableDatesAsync(symbol, ct);
        return Ok(dates);
    }

    /// <summary>
    /// Returns historical price and Greeks trajectory for a specific OCC option symbol
    /// </summary>
    [HttpGet("quotes/{optionSymbol}")]
    public async Task<ActionResult<List<HistoricalOptionSnapshot>>> GetOptionQuotes(
        string optionSymbol,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct)
    {
        var quotes = await _optionRepo.GetQuotesByOptionSymbolAsync(optionSymbol, from, to, ct);
        return Ok(quotes);
    }
}
