using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Services.Configuration;

namespace MyTradingToolbox.Services.Clients;

public interface ITradierClient
{
    Task<(HistoricalStockCandle? Candle, List<HistoricalOptionSnapshot> Snapshots)> FetchDailyEodAsync(string symbol, DateOnly? date = null, CancellationToken ct = default);
    Task<List<HistoricalStockCandle>> FetchHistoricalStockCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
}

public class TradierClient : ITradierClient
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataSettings _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<TradierClient> _logger;

    public TradierClient(
        HttpClient httpClient, 
        IOptions<MarketDataSettings> settings, 
        IConfiguration config,
        ILogger<TradierClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _config = config;
        _logger = logger;

        var baseUrl = _config["MarketData:TradierBaseUrl"] 
            ?? Environment.GetEnvironmentVariable("TRADIER_BASE_URL") 
            ?? _settings.TradierBaseUrl;

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private string GetApiToken()
    {
        return _config["MarketData:TradierApiToken"] 
            ?? _config["TRADIER_API_TOKEN"] 
            ?? _config["TRADIER_TOKEN"] 
            ?? Environment.GetEnvironmentVariable("TRADIER_API_TOKEN") 
            ?? Environment.GetEnvironmentVariable("TRADIER_TOKEN") 
            ?? _settings.TradierApiToken 
            ?? string.Empty;
    }

    private void EnsureAuthorizationHeader()
    {
        var token = GetApiToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Tradier API Token is not configured. Please set TRADIER_API_TOKEN in your environment variables.");
        }
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
    }

    public async Task<(HistoricalStockCandle? Candle, List<HistoricalOptionSnapshot> Snapshots)> FetchDailyEodAsync(string symbol, DateOnly? date = null, CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        symbol = symbol.Trim().ToUpperInvariant();

        EnsureAuthorizationHeader();

        // 1. Fetch Real Underlying Stock Quote from Tradier
        var quoteResponse = await _httpClient.GetAsync($"markets/quotes?symbols={symbol}", ct);
        var quoteJson = await quoteResponse.Content.ReadAsStringAsync(ct);

        if (!quoteResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Tradier quote API returned HTTP {StatusCode}: {Body}", quoteResponse.StatusCode, quoteJson);
            throw new HttpRequestException($"Tradier Quote API returned HTTP {quoteResponse.StatusCode}: {quoteJson}");
        }

        decimal underlyingPrice = 0m;
        decimal open = 0m, high = 0m, low = 0m, close = 0m;
        long volume = 0;

        using (var doc = JsonDocument.Parse(quoteJson))
        {
            if (!doc.RootElement.TryGetProperty("quotes", out var quotesProp) || 
                !quotesProp.TryGetProperty("quote", out var quoteElemWrapper))
            {
                throw new InvalidOperationException($"Tradier did not return quotes data for {symbol}");
            }

            var quoteElem = quoteElemWrapper.ValueKind == JsonValueKind.Array ? quoteElemWrapper[0] : quoteElemWrapper;
            
            close = quoteElem.TryGetProperty("last", out var lastProp) && lastProp.ValueKind == JsonValueKind.Number ? lastProp.GetDecimal() :
                    quoteElem.TryGetProperty("close", out var closeProp) && closeProp.ValueKind == JsonValueKind.Number ? closeProp.GetDecimal() : 0m;
            open = quoteElem.TryGetProperty("open", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetDecimal() : close;
            high = quoteElem.TryGetProperty("high", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDecimal() : close;
            low = quoteElem.TryGetProperty("low", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetDecimal() : close;
            volume = quoteElem.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
            underlyingPrice = close > 0 ? close : open;
        }

        var candle = new HistoricalStockCandle
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Date = targetDate,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume,
            Vwap = (open + high + low + close) / 4m,
            DataSource = DataSource.Tradier
        };

        // 2. Fetch Real Options Chain with Greeks from Tradier
        var chainResponse = await _httpClient.GetAsync($"markets/options/chains?symbol={symbol}&greeks=true", ct);
        var chainJson = await chainResponse.Content.ReadAsStringAsync(ct);

        if (!chainResponse.IsSuccessStatusCode)
        {
            _logger.LogError("Tradier option chains API returned HTTP {StatusCode}: {Body}", chainResponse.StatusCode, chainJson);
            throw new HttpRequestException($"Tradier Option Chain API returned HTTP {chainResponse.StatusCode}: {chainJson}");
        }

        var snapshots = new List<HistoricalOptionSnapshot>();
        using (var doc = JsonDocument.Parse(chainJson))
        {
            if (doc.RootElement.TryGetProperty("options", out var optionsProp) &&
                optionsProp.TryGetProperty("option", out var optionArrayProp))
            {
                var optionsList = optionArrayProp.ValueKind == JsonValueKind.Array ? optionArrayProp.EnumerateArray() : new[] { optionArrayProp }.AsEnumerable();

                foreach (var opt in optionsList)
                {
                    var optSymbol = opt.GetProperty("symbol").GetString() ?? "";
                    var strike = opt.GetProperty("strike").GetDecimal();
                    var optType = opt.GetProperty("option_type").GetString() ?? "call";
                    var expStr = opt.GetProperty("expiration_date").GetString() ?? "";
                    if (!DateOnly.TryParse(expStr, out var expDate)) continue;

                    var dte = (expDate.ToDateTime(TimeOnly.MinValue) - targetDate.ToDateTime(TimeOnly.MinValue)).Days;
                    if (dte < 0) continue;

                    var bid = opt.TryGetProperty("bid", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetDecimal() : 0m;
                    var ask = opt.TryGetProperty("ask", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetDecimal() : 0m;
                    var last = opt.TryGetProperty("last", out var lst) && lst.ValueKind == JsonValueKind.Number ? lst.GetDecimal() : 0m;
                    var optVol = opt.TryGetProperty("volume", out var ov) && ov.ValueKind == JsonValueKind.Number ? ov.GetInt64() : 0;
                    var oi = opt.TryGetProperty("open_interest", out var oip) && oip.ValueKind == JsonValueKind.Number ? oip.GetInt64() : 0;

                    decimal? delta = null, gamma = null, theta = null, vega = null, rho = null, iv = null;
                    if (opt.TryGetProperty("greeks", out var greeks) && greeks.ValueKind == JsonValueKind.Object)
                    {
                        if (greeks.TryGetProperty("delta", out var dVal) && dVal.ValueKind == JsonValueKind.Number) delta = dVal.GetDecimal();
                        if (greeks.TryGetProperty("gamma", out var gVal) && gVal.ValueKind == JsonValueKind.Number) gamma = gVal.GetDecimal();
                        if (greeks.TryGetProperty("theta", out var tVal) && tVal.ValueKind == JsonValueKind.Number) theta = tVal.GetDecimal();
                        if (greeks.TryGetProperty("vega", out var vVal) && vVal.ValueKind == JsonValueKind.Number) vega = vVal.GetDecimal();
                        if (greeks.TryGetProperty("rho", out var rVal) && rVal.ValueKind == JsonValueKind.Number) rho = rVal.GetDecimal();
                        if (greeks.TryGetProperty("mid_iv", out var ivVal) && ivVal.ValueKind == JsonValueKind.Number) iv = ivVal.GetDecimal();
                    }

                    snapshots.Add(new HistoricalOptionSnapshot
                    {
                        Id = Guid.NewGuid(),
                        UnderlyingSymbol = symbol,
                        SnapshotDate = targetDate,
                        OptionSymbol = optSymbol,
                        ExpirationDate = expDate,
                        DTE = dte,
                        Strike = strike,
                        Side = optType.Equals("put", StringComparison.OrdinalIgnoreCase) ? OptionSide.Put : OptionSide.Call,
                        Bid = bid,
                        Ask = ask,
                        Mid = (bid + ask) / 2m,
                        Last = last,
                        Delta = delta,
                        Gamma = gamma,
                        Theta = theta,
                        Vega = vega,
                        Rho = rho,
                        ImpliedVolatility = iv,
                        UnderlyingPrice = underlyingPrice,
                        Volume = optVol,
                        OpenInterest = oi,
                        DataSource = DataSource.Tradier
                    });
                }
            }
        }

        return (candle, snapshots);
    }

    public async Task<List<HistoricalStockCandle>> FetchHistoricalStockCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        EnsureAuthorizationHeader();

        var url = $"markets/history?symbol={symbol}&interval=daily&start={from:yyyy-MM-dd}&end={to:yyyy-MM-dd}";
        var response = await _httpClient.GetAsync(url, ct);
        var json = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to fetch stock candles for {Symbol} via Tradier API: {Body}", symbol, json);
            throw new HttpRequestException($"Tradier Stock History API returned HTTP {response.StatusCode}: {json}");
        }

        var candles = new List<HistoricalStockCandle>();
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("history", out var hist) && hist.TryGetProperty("day", out var days))
        {
            var dayArray = days.ValueKind == JsonValueKind.Array ? days.EnumerateArray() : new[] { days }.AsEnumerable();
            foreach (var d in dayArray)
            {
                var dateStr = d.GetProperty("date").GetString() ?? "";
                if (!DateOnly.TryParse(dateStr, out var cDate)) continue;

                var open = d.GetProperty("open").GetDecimal();
                var high = d.GetProperty("high").GetDecimal();
                var low = d.GetProperty("low").GetDecimal();
                var close = d.GetProperty("close").GetDecimal();
                var vol = d.GetProperty("volume").GetInt64();
                var vwap = d.TryGetProperty("vwap", out var vw) && vw.ValueKind == JsonValueKind.Number ? vw.GetDecimal() : (open + high + low + close) / 4m;

                candles.Add(new HistoricalStockCandle
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    Date = cDate,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = vol,
                    Vwap = vwap,
                    DataSource = DataSource.Tradier
                });
            }
        }

        return candles;
    }
}
