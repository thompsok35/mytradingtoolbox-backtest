using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
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
    private readonly IConfiguration _config;
    private readonly ILogger<MarketDataClient> _logger;

    public MarketDataClient(
        HttpClient httpClient, 
        IOptions<MarketDataSettings> settings, 
        IConfiguration config,
        ILogger<MarketDataClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _config = config;
        _logger = logger;

        var baseUrl = _config["MarketData:MarketDataBaseUrl"] 
            ?? _config["MARKETDATA_BASE_URL"] 
            ?? Environment.GetEnvironmentVariable("MARKETDATA_BASE_URL") 
            ?? _settings.MarketDataBaseUrl;

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private void EnsureAuth()
    {
        var token = _config["MarketData:MarketDataApiToken"] 
            ?? _config["MARKETDATA_API_TOKEN"] 
            ?? _config["MARKETDATA_TOKEN"] 
            ?? Environment.GetEnvironmentVariable("MARKETDATA_API_TOKEN") 
            ?? Environment.GetEnvironmentVariable("MARKETDATA_TOKEN") 
            ?? _settings.MarketDataApiToken;

        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        }
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchFilteredOptionsAsync(string symbol, DateOnly date, int minDte = 7, int maxDte = 45, OptionSide side = OptionSide.Call, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        EnsureAuth();

        var dateStr = $"{date:yyyy-MM-dd}";
        var sideStr = side == OptionSide.Call ? "call" : "put";
        var url = $"options/chain/{symbol}/?date={dateStr}&side={sideStr}";

        var snapshots = new List<HistoricalOptionSnapshot>();

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"MarketData.app API returned HTTP {response.StatusCode}: {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("s", out var s) && s.GetString() == "ok")
            {
                var optSymbols = root.TryGetProperty("optionSymbol", out var os) ? os.EnumerateArray() : default;
                var expirations = root.TryGetProperty("expiration", out var exp) ? exp.EnumerateArray() : default;
                var strikes = root.TryGetProperty("strike", out var stk) ? stk.EnumerateArray() : default;
                var bids = root.TryGetProperty("bid", out var bd) ? bd.EnumerateArray() : default;
                var asks = root.TryGetProperty("ask", out var ak) ? ak.EnumerateArray() : default;
                var mids = root.TryGetProperty("mid", out var md) ? md.EnumerateArray() : default;
                var deltas = root.TryGetProperty("delta", out var dl) ? dl.EnumerateArray() : default;
                var ivs = root.TryGetProperty("iv", out var ivElem) ? ivElem.EnumerateArray() : default;
                var undPrices = root.TryGetProperty("underlyingPrice", out var up) ? up.EnumerateArray() : default;

                while (optSymbols.MoveNext())
                {
                    var optSym = optSymbols.Current.GetString() ?? "";
                    
                    DateOnly expDate = date.AddDays(7);
                    if (expirations.MoveNext())
                    {
                        if (expirations.Current.ValueKind == JsonValueKind.Number)
                        {
                            var unix = expirations.Current.GetInt64();
                            expDate = DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unix).UtcDateTime);
                        }
                        else if (expirations.Current.ValueKind == JsonValueKind.String && DateOnly.TryParse(expirations.Current.GetString(), out var parsedExp))
                        {
                            expDate = parsedExp;
                        }
                    }

                    var strike = strikes.MoveNext() ? strikes.Current.GetDecimal() : 0m;
                    var bid = bids.MoveNext() && bids.Current.ValueKind == JsonValueKind.Number ? bids.Current.GetDecimal() : 0m;
                    var ask = asks.MoveNext() && asks.Current.ValueKind == JsonValueKind.Number ? asks.Current.GetDecimal() : 0m;
                    var mid = mids.MoveNext() && mids.Current.ValueKind == JsonValueKind.Number ? mids.Current.GetDecimal() : (bid + ask) / 2m;
                    var delta = deltas.MoveNext() && deltas.Current.ValueKind == JsonValueKind.Number ? deltas.Current.GetDecimal() : (decimal?)null;
                    var iv = ivs.MoveNext() && ivs.Current.ValueKind == JsonValueKind.Number ? ivs.Current.GetDecimal() : (decimal?)null;
                    var undPrice = undPrices.MoveNext() && undPrices.Current.ValueKind == JsonValueKind.Number ? undPrices.Current.GetDecimal() : 0m;

                    var dte = (expDate.ToDateTime(TimeOnly.MinValue) - date.ToDateTime(TimeOnly.MinValue)).Days;
                    if (dte < minDte || dte > maxDte) continue;

                    snapshots.Add(new HistoricalOptionSnapshot
                    {
                        Id = Guid.NewGuid(),
                        UnderlyingSymbol = symbol,
                        SnapshotDate = date,
                        OptionSymbol = optSym,
                        ExpirationDate = expDate,
                        DTE = dte,
                        Strike = strike,
                        Side = side,
                        Bid = bid,
                        Ask = ask,
                        Mid = mid,
                        Last = mid,
                        Delta = delta,
                        ImpliedVolatility = iv,
                        UnderlyingPrice = undPrice,
                        Volume = 0,
                        OpenInterest = 0,
                        DataSource = DataSource.MarketData
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching from MarketData.app for {Symbol} on {Date}", symbol, date);
            throw;
        }

        return snapshots;
    }
}
