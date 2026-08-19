using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Backtest;
using MyTradingToolbox.Services.Clients;
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

        // Seed 6 months of deterministic market data for AAPL
        var start = new DateOnly(2025, 1, 1);
        var end = new DateOnly(2025, 6, 30);
        var candles = TradierClient.GenerateSimulatedHistoricalCandles("AAPL", start, end);
        await candleRepo.UpsertCandlesAsync(candles);

        foreach (var c in candles)
        {
            var (_, snaps) = TradierClient.GenerateSimulatedEodData("AAPL", c.Date, c.Close);
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
        result.DailyEquityCurve.Should().NotBeEmpty();
        result.Metrics.TotalTrades.Should().Be(result.Trades.Count);
        result.Metrics.FinalEquity.Should().BeGreaterThan(0);
        result.DailyEquityCurve.First().TotalEquity.Should().BeInRange(49000m, 51000m);
        result.Metrics.CAGRPercent.Should().NotBe(0);

        foreach (var trade in result.Trades)
        {
            trade.Contracts.Should().BeGreaterThan(0);
            trade.StockEntryPrice.Should().BeGreaterThan(0);
            trade.Strike.Should().BeGreaterThan(0);
            trade.HoldDays.Should().BeGreaterThan(0);
            trade.ExitReason.Should().BeDefined();
        }
    }
}
