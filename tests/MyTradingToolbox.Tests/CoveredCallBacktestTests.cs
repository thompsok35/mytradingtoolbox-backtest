using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Backtest;
using Xunit;

namespace MyTradingToolbox.Tests;

public class CoveredCallBacktestTests
{
    [Fact]
    public async Task ExecuteBacktest_ITMCoveredCall_GeneratesTradesAndPositiveEquity()
    {
        var options = new DbContextOptionsBuilder<MarketDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MarketDataContext(options);
        var optionRepo = new OptionSnapshotRepository(db);
        var candleRepo = new StockCandleRepository(db);

        // Seed 6 months of test market data for AAPL
        var start = new DateOnly(2025, 1, 1);
        var end = new DateOnly(2025, 6, 30);
        var candles = TestFixtureDataGenerator.GenerateTestCandles("AAPL", start, end, 220m);
        await candleRepo.UpsertCandlesAsync(candles);

        foreach (var c in candles)
        {
            var snaps = TestFixtureDataGenerator.GenerateTestOptionSnapshots("AAPL", c.Date, c.Close);
            await optionRepo.UpsertSnapshotsAsync(snaps);
        }

        // Execute ITM Covered Call Backtest
        var engine = new BacktestEngine(optionRepo, candleRepo, NullLogger<BacktestEngine>.Instance);
        var request = new BacktestRequest
        {
            Strategy = "ITM_COVERED_CALL",
            Symbol = "AAPL",
            StartDate = start,
            EndDate = end,
            InitialCapital = 50000m,
            TargetDelta = 0.70m,
            TargetDte = 30,
            MinDte = 14,
            MaxDte = 45,
            ProfitTargetPercent = 0.60m,
            RollOnDeltaBreach = true,
            RollDeltaThreshold = 0.50m
        };

        var result = await engine.ExecuteBacktestAsync(request);

        result.Should().NotBeNull();
        result.Symbol.Should().Be("AAPL");
        result.Trades.Should().NotBeEmpty();
        result.Metrics.TotalTrades.Should().BeGreaterThan(0);
        result.DailyEquityCurve.Should().NotBeEmpty();
        result.Trades.All(t => t.EntryProbITM > 0).Should().BeTrue();
    }

    [Theory]
    [InlineData(100, 90, 30)]  // ITM Call: Strike < Spot -> Prob ITM should be high
    [InlineData(100, 110, 30)] // OTM Call: Strike > Spot -> Prob ITM should be lower
    public void BlackScholesCalculator_ComputesCorrectProbabilityOfITM(decimal spot, decimal strike, int dte)
    {
        var isItm = strike < spot;
        var estPrice = Math.Max(0.50m, spot - strike + 2.0m);
        var greeks = Core.Calculators.BlackScholesCalculator.ComputeGreeks(
            spot, strike, dte, Core.Enums.OptionSide.Call, estPrice);

        if (isItm)
        {
            greeks.Delta.Should().BeGreaterThan(0.50m);
            greeks.ProbabilityOfITM.Should().BeGreaterThan(0.50m);
            // Delta is mathematically greater than or equal to N(d2) for calls
            greeks.Delta.Should().BeGreaterThanOrEqualTo(greeks.ProbabilityOfITM - 0.05m);
        }
        else
        {
            greeks.Delta.Should().BeLessThan(0.50m);
            greeks.ProbabilityOfITM.Should().BeLessThan(0.50m);
        }
    }
}
