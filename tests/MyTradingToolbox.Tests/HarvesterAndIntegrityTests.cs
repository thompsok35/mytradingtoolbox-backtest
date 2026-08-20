using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Configuration;
using MyTradingToolbox.Services.Harvester;
using MyTradingToolbox.Services.Integrity;
using Xunit;

namespace MyTradingToolbox.Tests;

public class HarvesterAndIntegrityTests
{
    [Fact]
    public void IsUsMarketTradingDay_FiltersWeekendsAndHolidays()
    {
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 1)).Should().BeFalse(); // New Year
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 4)).Should().BeFalse(); // Saturday
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 5)).Should().BeFalse(); // Sunday
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 1, 6)).Should().BeTrue();  // Monday
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 7, 4)).Should().BeFalse(); // Independence Day
        DataIntegrityService.IsUsMarketTradingDay(new DateOnly(2025, 12, 25)).Should().BeFalse(); // Christmas
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
        var candles = TestFixtureDataGenerator.GenerateTestCandles("SPY", start, end, 550m);
        await candleRepo.UpsertCandlesAsync(candles);

        foreach (var c in candles)
        {
            var snaps = TestFixtureDataGenerator.GenerateTestOptionSnapshots("SPY", c.Date, c.Close);
            await optionRepo.UpsertSnapshotsAsync(snaps);
        }

        await watchlistRepo.AddOrUpdateAsync(new WatchlistSymbol { Symbol = "SPY" });
        await watchlistRepo.UpdateCoverageStatsAsync("SPY");

        var config = new ConfigurationBuilder().Build();
        var settings = Options.Create(new MarketDataSettings());

        var tradierClient = new TradierClient(new HttpClient(), settings, config, NullLogger<TradierClient>.Instance);
        var thetaClient = new ThetaDataClient(new HttpClient(), settings, config, NullLogger<ThetaDataClient>.Instance);
        var marketDataClient = new MarketDataClient(new HttpClient(), settings, config, NullLogger<MarketDataClient>.Instance);

        var harvester = new HarvestOrchestrator(
            watchlistRepo, optionRepo, candleRepo, jobRepo,
            tradierClient, thetaClient, marketDataClient,
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
