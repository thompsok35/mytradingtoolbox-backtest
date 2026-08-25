using System.Net;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Configuration;
using Xunit;

namespace MyTradingToolbox.Tests;

public class MockHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        _handler = handler;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_handler(request));
    }
}

public class ThetaDataClientTests
{
    [Fact]
    public async Task FetchEodHistoricalRangeAsync_ParsesCsvResponseCorrectly()
    {
        var csvData = @"date,expiration,strike,right,open,high,low,close,volume,bid,ask,open_interest
20250102,20250117,130000,C,5.20,5.80,4.90,5.50,1500,5.40,5.60,25000
20250102,20250117,130000,P,2.10,2.30,1.90,2.00,800,1.95,2.05,12000";

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csvData)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var settings = Options.Create(new MarketDataSettings());
        var config = new ConfigurationBuilder().Build();
        var client = new ThetaDataClient(httpClient, settings, config, NullLogger<ThetaDataClient>.Instance);

        var results = await client.FetchEodHistoricalRangeAsync("NVDA", new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 3));

        results.Should().NotBeNull();
        results.Should().HaveCount(2);

        var call = results.First(r => r.Side == OptionSide.Call);
        call.UnderlyingSymbol.Should().Be("NVDA");
        call.SnapshotDate.Should().Be(new DateOnly(2025, 1, 2));
        call.ExpirationDate.Should().Be(new DateOnly(2025, 1, 17));
        call.Strike.Should().Be(130m);
        call.Last.Should().Be(5.50m);
        call.Bid.Should().Be(5.40m);
        call.Ask.Should().Be(5.60m);
        call.Mid.Should().Be(5.50m);
        call.Volume.Should().Be(1500);
        call.OpenInterest.Should().Be(25000);
        call.OptionSymbol.Should().Be("NVDA250117C00130000");

        var put = results.First(r => r.Side == OptionSide.Put);
        put.OptionSymbol.Should().Be("NVDA250117P00130000");
        put.Bid.Should().Be(1.95m);
        put.Ask.Should().Be(2.05m);
    }

    [Fact]
    public async Task FetchEodHistoricalRangeAsync_ParsesJsonResponseCorrectly()
    {
        var jsonData = @"{
            ""response"": [
                {
                    ""contract"": {
                        ""root"": ""NVDA"",
                        ""expiration"": 20250117,
                        ""strike"": 130000,
                        ""right"": ""C""
                    },
                    ""data"": [
                        {
                            ""date"": 20250102,
                            ""close"": 5.50,
                            ""low"": 4.90,
                            ""high"": 5.80,
                            ""bid"": 5.40,
                            ""ask"": 5.60,
                            ""volume"": 1500,
                            ""open_interest"": 25000
                        }
                    ]
                }
            ]
        }";

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonData)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var settings = Options.Create(new MarketDataSettings());
        var config = new ConfigurationBuilder().Build();
        var client = new ThetaDataClient(httpClient, settings, config, NullLogger<ThetaDataClient>.Instance);

        var results = await client.FetchEodHistoricalRangeAsync("NVDA", new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 3));

        results.Should().HaveCount(1);
        var snap = results[0];
        snap.UnderlyingSymbol.Should().Be("NVDA");
        snap.Strike.Should().Be(130m);
        snap.Side.Should().Be(OptionSide.Call);
        snap.Last.Should().Be(5.50m);
        snap.Bid.Should().Be(5.40m);
        snap.Ask.Should().Be(5.60m);
    }

    [Fact]
    public async Task FetchHistoricalStockCandlesAsync_ParsesStockDataCorrectly()
    {
        var csvData = @"date,open,high,low,close,volume
20250102,135.00,138.50,134.20,137.80,45000000";

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csvData)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var settings = Options.Create(new MarketDataSettings());
        var config = new ConfigurationBuilder().Build();
        var client = new ThetaDataClient(httpClient, settings, config, NullLogger<ThetaDataClient>.Instance);

        var candles = await client.FetchHistoricalStockCandlesAsync("NVDA", new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 3));

        candles.Should().HaveCount(1);
        candles[0].Symbol.Should().Be("NVDA");
        candles[0].Close.Should().Be(137.80m);
        candles[0].Volume.Should().Be(45000000);
    }

    [Fact]
    public async Task FetchEodHistoricalRangeAsync_ParsesIsoHyphenatedDatesAndHeaderAliasesCorrectly()
    {
        // Tests ThetaData CSV returning ISO date strings (e.g. "2025-01-02", "2025-01-03") and "created" column header
        var csvData = @"created,expiration,strike,right,open,high,low,close,volume,bid,ask,open_interest
2025-01-02,2025-01-17,130.00,CALL,5.20,5.80,4.90,5.50,1500,5.40,5.60,25000
2025-01-03,2025-01-17,130.00,CALL,5.50,6.10,5.30,6.00,2200,5.90,6.10,25500
2025-01-06,2025-01-17,130.00,PUT,2.10,2.30,1.90,2.00,800,1.95,2.05,12000";

        var mockHandler = new MockHttpMessageHandler(req =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(csvData)
            };
        });

        var httpClient = new HttpClient(mockHandler);
        var settings = Options.Create(new MarketDataSettings());
        var config = new ConfigurationBuilder().Build();
        var client = new ThetaDataClient(httpClient, settings, config, NullLogger<ThetaDataClient>.Instance);

        var results = await client.FetchEodHistoricalRangeAsync("NVDA", new DateOnly(2025, 1, 2), new DateOnly(2025, 1, 7));

        results.Should().HaveCount(3);

        // Verify that distinct snapshot dates are preserved properly across trading days (not collapsed into chunkStart)
        var dates = results.Select(r => r.SnapshotDate).Distinct().OrderBy(d => d).ToList();
        dates.Should().HaveCount(3);
        dates[0].Should().Be(new DateOnly(2025, 1, 2));
        dates[1].Should().Be(new DateOnly(2025, 1, 3));
        dates[2].Should().Be(new DateOnly(2025, 1, 6));

        // Verify expiration date parsing
        results.All(r => r.ExpirationDate == new DateOnly(2025, 1, 17)).Should().BeTrue();
    }
}
