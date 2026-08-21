using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Utils;
using MyTradingToolbox.Services.Configuration;

namespace MyTradingToolbox.Services.Clients;

public interface IThetaDataClient
{
    Task<bool> TestTerminalConnectionAsync(CancellationToken ct = default);
    Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<List<HistoricalStockCandle>> FetchHistoricalStockCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<List<DateOnly>> FetchAvailableExpirationsAsync(string symbol, CancellationToken ct = default);
}

public class ThetaDataClient : IThetaDataClient
{
    private readonly HttpClient _httpClient;
    private readonly MarketDataSettings _settings;
    private readonly IConfiguration _config;
    private readonly ILogger<ThetaDataClient> _logger;

    public ThetaDataClient(
        HttpClient httpClient, 
        IOptions<MarketDataSettings> settings, 
        IConfiguration config,
        ILogger<ThetaDataClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _config = config;
        _logger = logger;

        var baseUrl = _config["MarketData:ThetaDataBaseUrl"] 
            ?? _config["THETADATA_BASE_URL"] 
            ?? Environment.GetEnvironmentVariable("THETADATA_BASE_URL") 
            ?? _settings.ThetaDataBaseUrl;

        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains(":25510"))
        {
            baseUrl = "http://127.0.0.1:25503/v3/";
        }

        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        _httpClient.BaseAddress = new Uri(baseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
    }

    public async Task<bool> TestTerminalConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _httpClient.GetAsync("stock/list/symbols", ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ThetaData Terminal connection test failed at {BaseAddress}", _httpClient.BaseAddress);
            return false;
        }
    }

    public async Task<List<DateOnly>> FetchAvailableExpirationsAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var expirations = new List<DateOnly>();

        try
        {
            var url = $"option/list/expirations?symbol={symbol}";
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ThetaData v3 option/list/expirations failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement expArray = root;
            if (root.TryGetProperty("response", out var rProp))
            {
                expArray = rProp;
            }

            if (expArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in expArray.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var intDate))
                    {
                        expirations.Add(new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100));
                    }
                    else if (item.ValueKind == JsonValueKind.String && DateOnly.TryParse(item.GetString(), out var d))
                    {
                        expirations.Add(d);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expirations from ThetaData v3 for {Symbol}", symbol);
            throw;
        }

        return expirations;
    }

    public async Task<List<HistoricalStockCandle>> FetchHistoricalStockCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var candles = new List<HistoricalStockCandle>();

        var startStr = $"{from:yyyyMMdd}";
        var endStr = $"{to:yyyyMMdd}";
        var url = $"stock/history/eod?symbol={symbol}&start_date={startStr}&end_date={endStr}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ThetaData v3 stock history failed: {Url} -> HTTP {Status}: {Body}", url, response.StatusCode, json);
                throw new HttpRequestException($"ThetaData Terminal stock history failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var items = root.TryGetProperty("response", out var rProp) ? rProp : root;
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        DateOnly cDate = from;
                        if (item.TryGetProperty("date", out var dElem))
                        {
                            if (dElem.ValueKind == JsonValueKind.Number)
                            {
                                int intDate = dElem.GetInt32();
                                cDate = new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);
                            }
                            else if (DateOnly.TryParse(dElem.GetString(), out var pd))
                            {
                                cDate = pd;
                            }
                        }

                        decimal open = item.TryGetProperty("open", out var o) ? o.GetDecimal() : 0m;
                        decimal high = item.TryGetProperty("high", out var h) ? h.GetDecimal() : 0m;
                        decimal low = item.TryGetProperty("low", out var l) ? l.GetDecimal() : 0m;
                        decimal close = item.TryGetProperty("close", out var c) ? c.GetDecimal() : 0m;
                        long vol = item.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

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
                            Vwap = (open + high + low + close) / 4m,
                            DataSource = DataSource.ThetaData
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve stock candles from ThetaData Terminal for {Symbol}", symbol);
            throw;
        }

        return candles;
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        _logger.LogInformation("Retrieving real historical option chains from ThetaData Terminal v3 for {Symbol} from {From} to {To}", symbol, from, to);

        var snapshots = new List<HistoricalOptionSnapshot>();
        var startStr = $"{from:yyyyMMdd}";
        var endStr = $"{to:yyyyMMdd}";

        // ThetaData v3 Option EOD History: /v3/option/history/eod?symbol={symbol}&expiration=*&start_date={start}&end_date={end}
        var url = $"option/history/eod?symbol={symbol}&expiration=*&start_date={startStr}&end_date={endStr}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ThetaData v3 option/history/eod failed: HTTP {Status}: {Body}", response.StatusCode, json);
                throw new HttpRequestException($"ThetaData Terminal v3 options failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var items = root.TryGetProperty("response", out var rProp) ? rProp : root;
            if (items.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in items.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.Object)
                    {
                        DateOnly snapDate = from;
                        if (item.TryGetProperty("date", out var dElem))
                        {
                            if (dElem.ValueKind == JsonValueKind.Number)
                            {
                                int intDate = dElem.GetInt32();
                                snapDate = new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);
                            }
                            else if (DateOnly.TryParse(dElem.GetString(), out var pd))
                            {
                                snapDate = pd;
                            }
                        }

                        DateOnly expDate = snapDate.AddDays(7);
                        if (item.TryGetProperty("expiration", out var expElem))
                        {
                            if (expElem.ValueKind == JsonValueKind.Number)
                            {
                                int intExp = expElem.GetInt32();
                                expDate = new DateOnly(intExp / 10000, (intExp % 10000) / 100, intExp % 100);
                            }
                            else if (DateOnly.TryParse(expElem.GetString(), out var pexp))
                            {
                                expDate = pexp;
                            }
                        }

                        var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                        if (dte < 0) continue;

                        decimal strike = item.TryGetProperty("strike", out var stk) ? stk.GetDecimal() : 0m;
                        if (strike > 1000) strike /= 1000m; // Handle mills format

                        var rightStr = item.TryGetProperty("right", out var rElem) ? rElem.GetString()?.ToUpperInvariant() ?? "C" : "C";
                        var side = rightStr.StartsWith("P") ? OptionSide.Put : OptionSide.Call;

                        decimal close = item.TryGetProperty("close", out var cElem) ? cElem.GetDecimal() : 0m;
                        decimal low = item.TryGetProperty("low", out var lElem) ? lElem.GetDecimal() : 0m;
                        decimal high = item.TryGetProperty("high", out var hElem) ? hElem.GetDecimal() : 0m;
                        long vol = item.TryGetProperty("volume", out var vElem) && vElem.ValueKind == JsonValueKind.Number ? vElem.GetInt64() : 0;

                        decimal bid = Math.Max(0.01m, low);
                        decimal ask = Math.Max(bid, high > 0 ? high : close);
                        decimal mid = (bid + ask) / 2m;

                        var optSymbol = OCCParser.Format(symbol, expDate, side, strike);

                        snapshots.Add(new HistoricalOptionSnapshot
                        {
                            Id = Guid.NewGuid(),
                            UnderlyingSymbol = symbol,
                            SnapshotDate = snapDate,
                            OptionSymbol = optSymbol,
                            ExpirationDate = expDate,
                            DTE = dte,
                            Strike = strike,
                            Side = side,
                            Bid = bid,
                            Ask = ask,
                            Mid = mid,
                            Last = close,
                            UnderlyingPrice = 0m,
                            Volume = vol,
                            OpenInterest = 0,
                            DataSource = DataSource.ThetaData
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch bulk options from ThetaData Terminal for {Symbol}", symbol);
            throw new InvalidOperationException($"ThetaData Terminal v3 error for {symbol}: {ex.Message}. Make sure ThetaTerminal is running on port 25503 and logged in.", ex);
        }

        return snapshots;
    }
}
