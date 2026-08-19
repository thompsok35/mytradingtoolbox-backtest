using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Data.Context;
using MyTradingToolbox.Data.Repositories;
using MyTradingToolbox.Services.Harvester;
using Xunit;

namespace MyTradingToolbox.Tests;

public class CSVImporterTests
{
    [Fact]
    public async Task ImportCsv_StandardCsvFormat_ParsesAndInsertsRecords()
    {
        var options = new DbContextOptionsBuilder<MarketDataContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var db = new MarketDataContext(options);
        var optionRepo = new OptionSnapshotRepository(db);
        var candleRepo = new StockCandleRepository(db);
        var watchlistRepo = new WatchlistRepository(db);

        var service = new CSVImporterService(optionRepo, candleRepo, watchlistRepo, NullLogger<CSVImporterService>.Instance);

        var csvContent = @"OptionSymbol,UnderlyingSymbol,SnapshotDate,ExpirationDate,Strike,Side,Bid,Ask,UnderlyingPrice,Delta,ImpliedVolatility
AAPL250620C00200000,AAPL,2025-05-01,2025-06-20,200.00,Call,12.50,12.80,210.00,0.72,0.285
AAPL250620P00200000,AAPL,2025-05-01,2025-06-20,200.00,Put,2.10,2.25,210.00,-0.28,0.285
";

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var (snapshotsInserted, candlesInserted, report) = await service.ImportCsvAsync(stream);

        snapshotsInserted.Should().Be(2);
        candlesInserted.Should().Be(1);
        report.Should().Contain("CSV Ingestion complete");

        var chain = await optionRepo.GetChainAsync(new OptionChainFilter { Symbol = "AAPL", Date = new DateOnly(2025, 5, 1) });
        chain.Should().HaveCount(2);
        chain.First(c => c.Side == Core.Enums.OptionSide.Call).Strike.Should().Be(200.00m);
        chain.First(c => c.Side == Core.Enums.OptionSide.Call).Delta.Should().Be(0.72m);
    }
}
