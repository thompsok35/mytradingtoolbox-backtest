using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Harvester;
using MyTradingToolbox.Services.Integrity;
using Xunit;

namespace MyTradingToolbox.Tests;

public class HarvesterAndIntegrityTests
{
    [Fact]
    public void IsUsMarketTradingDay_FiltersWeekendsAndHolidays()
    {
        // Weekend
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 4)).Should().BeFalse(); // Saturday
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 5)).Should().BeFalse(); // Sunday

        // US Market Holidays
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 1)).Should().BeFalse(); // New Year's Day
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 7, 4)).Should().BeFalse(); // July 4th
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 12, 25)).Should().BeFalse(); // Christmas

        // Valid trading days
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 2)).Should().BeTrue(); // Thursday
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 6, 18)).Should().BeTrue(); // Wednesday
    }

    [Fact]
    public async Task AuditSymbol_DetectsCompletenessAndHealthScore()
    {
        var options = new DbContextOptionsBuilder<MarketDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MarketDataContext(options);
        var optionRepo = new OptionSnapshotRepository(db);
        var candleRepo = new StockCandleRepository(db);
        var watchlistRepo = new WatchlistRepository(db);
        var integrityRepo = new IntegrityRepository(db);
        var jobRepo = new HarvestJobRepository(db);

        // Seed 10 trading days for SPY
        var start = new DateOnly(2025, 3, 3);
        var end = new DateOnly(2025, 3, 14);
        var candles = TradierClient.GenerateSimulatedHistoricalCandles("SPY", start, end);
        await candleRepo.UpsertCandlesAsync(candles);

        foreach (var c in candles)
        {
            var (_, snaps) = TradierClient.GenerateSimulatedEodData("SPY", c.Date, c.Close);
            await optionRepo.UpsertSnapshotsAsync(snaps);
        }

        await watchlistRepo.AddOrUpdateAsync(new WatchlistSymbol { Symbol = "SPY" });
        await watchlistRepo.UpdateCoverageStatsAsync("SPY");

        var harvester = new HarvestOrchestrator(
            watchlistRepo, optionRepo, candleRepo, jobRepo,
            new TradierClient(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new Services.Configuration.MarketDataSettings { UseSimulatedDataIfNoToken = true }), NullLogger<TradierClient>.Instance),
            new ThetaDataClient(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new Services.Configuration.MarketDataSettings()), NullLogger<ThetaDataClient>.Instance),
            new MarketDataClient(new HttpClient(), Microsoft.Extensions.Options.Options.Create(new Services.Configuration.MarketDataSettings()), NullLogger<MarketDataClient>.Instance),
            NullLogger<HarvestOrchestrator>.Instance);

        var integrityService = new DataIntegrityService(
            optionRepo, candleRepo, watchlistRepo, integrityRepo, jobRepo, harvester, NullLogger<DataIntegrityService>.Instance);

        var audit = await integrityService.AuditSymbolAsync("SPY");

        audit.Should().NotBeNull();
        audit.Symbol.Should().Be("SPY");
        audit.TotalExpectedTradingDays.Should().Be(10);
        audit.ActualDaysPresent.Should().Be(10);
        audit.HealthScorePercent.Should().Be(100m);
        audit.CorruptQuotesCount.Should().Be(0);
    }
}
