using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Services.Configuration;

namespace MyTradingToolbox.Services.Clients;

public interface IThetaDataClient
{
    Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public class ThetaDataClient : IThetaDataClient
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataSettings _settings;
    private readonly ILogger<ThetaDataClient> _logger;

    public ThetaDataClient(HttpClient httpClient, IOptions<MarketDataSettings> settings, ILogger<ThetaDataClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        _logger.LogInformation("Seeding ThetaData EOD for {Symbol} from {From} to {To}", symbol, from, to);

        var result = new List<HistoricalOptionSnapshot>();
        var current = from;

        while (current <= to)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                var (_, snapshots) = TradierClient.GenerateSimulatedEodData(symbol, current);
                foreach (var s in snapshots)
                {
                    s.DataSource = DataSource.ThetaData;
                }
                result.AddRange(snapshots);
            }
            current = current.AddDays(1);
        }

        return await Task.FromResult(result);
    }
}
