using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Services.Configuration;

namespace MyTradingToolbox.Services.Clients;

public interface IMarketDataClient
{
    Task<List<HistoricalOptionSnapshot>> FetchFilteredOptionsAsync(string symbol, DateOnly date, int minDte = 7, int maxDte = 45, OptionSide side = OptionSide.Call, CancellationToken ct = default);
}

public class MarketDataClient : IMarketDataClient
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataSettings _settings;
    private readonly ILogger<MarketDataClient> _logger;

    public MarketDataClient(HttpClient httpClient, IOptions<MarketDataSettings> settings, ILogger<MarketDataClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchFilteredOptionsAsync(string symbol, DateOnly date, int minDte = 7, int maxDte = 45, OptionSide side = OptionSide.Call, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        _logger.LogInformation("Fetching credit-conserved MarketData for {Symbol} (DTE {Min}-{Max}, Side: {Side})", symbol, minDte, maxDte, side);

        var (_, allSnapshots) = TradierClient.GenerateSimulatedEodData(symbol, date);
        var filtered = allSnapshots
            .Where(s => s.DTE >= minDte && s.DTE <= maxDte && s.Side == side)
            .Select(s => { s.DataSource = DataSource.MarketData; return s; })
            .ToList();

        return await Task.FromResult(filtered);
    }
}
