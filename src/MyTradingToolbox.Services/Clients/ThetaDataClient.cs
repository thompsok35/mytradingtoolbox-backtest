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

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = "http://127.0.0.1:25510/v2/";
        }

        _httpClient.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _httpClient.Timeout = TimeSpan.FromSeconds(60);
    }

    public async Task<bool> TestTerminalConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _httpClient.GetAsync("list/roots?sec=option", ct);
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
            var response = await _httpClient.GetAsync($"list/expirations?root={symbol}", ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ThetaData list/expirations failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement expArray = root;
            if (root.TryGetProperty("response", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
            {
                expArray = rProp;
            }

            if (expArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in expArray.EnumerateArray())
                {
                    var intDate = item.GetInt32();
                    int y = intDate / 10000;
                    int m = (intDate % 10000) / 100;
                    int d = intDate % 100;
                    expirations.Add(new DateOnly(y, m, d));
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching expirations from ThetaData for {Symbol}", symbol);
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
        var url = $"hist/stock/eod?root={symbol}&start_date={startStr}&end_date={endStr}";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ThetaData stock hist call failed: {Url} -> HTTP {Status}: {Body}", url, response.StatusCode, json);
                throw new HttpRequestException($"ThetaData Terminal stock history failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var respArray) && respArray.ValueKind == JsonValueKind.Array)
            {
                // Find column mappings from header if available
                var headerList = new List<string>();
                if (root.TryGetProperty("header", out var hProp) && hProp.TryGetProperty("format", out var fArray))
                {
                    foreach (var h in fArray.EnumerateArray()) headerList.Add(h.GetString()?.ToLowerInvariant() ?? "");
                }

                int dateIdx = headerList.IndexOf("date");
                int openIdx = headerList.IndexOf("open");
                int highIdx = headerList.IndexOf("high");
                int lowIdx = headerList.IndexOf("low");
                int closeIdx = headerList.IndexOf("close");
                int volIdx = headerList.IndexOf("volume");

                if (dateIdx < 0) dateIdx = 7;
                if (openIdx < 0) openIdx = 1;
                if (highIdx < 0) highIdx = 2;
                if (lowIdx < 0) lowIdx = 3;
                if (closeIdx < 0) closeIdx = 4;
                if (volIdx < 0) volIdx = 5;

                foreach (var row in respArray.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array) continue;
                    var rowArr = row.EnumerateArray().ToArray();
                    if (rowArr.Length <= Math.Max(closeIdx, dateIdx)) continue;

                    int intDate = rowArr[dateIdx].GetInt32();
                    var cDate = new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);

                    decimal open = rowArr[openIdx].GetDecimal();
                    decimal high = rowArr[highIdx].GetDecimal();
                    decimal low = rowArr[lowIdx].GetDecimal();
                    decimal close = rowArr[closeIdx].GetDecimal();
                    long vol = rowArr[volIdx].GetInt64();

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve stock candles from ThetaData Terminal for {Symbol}", symbol);
            throw new InvalidOperationException($"ThetaData Terminal unavailable or error for {symbol}: {ex.Message}. Make sure ThetaData Terminal is running at {_httpClient.BaseAddress}.", ex);
        }

        return candles;
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        _logger.LogInformation("Retrieving real historical option chains from ThetaData Terminal for {Symbol} from {From} to {To}", symbol, from, to);

        var snapshots = new List<HistoricalOptionSnapshot>();
        var startStr = $"{from:yyyyMMdd}";
        var endStr = $"{to:yyyyMMdd}";

        // ThetaData Bulk EOD Option History: /v2/bulk_hist/option/eod?root={root}&start_date={start}&end_date={end}&exp=0
        var url = $"bulk_hist/option/eod?root={symbol}&start_date={startStr}&end_date={endStr}&exp=0";

        try
        {
            var response = await _httpClient.GetAsync(url, ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("ThetaData bulk option call failed: HTTP {Status}: {Body}", response.StatusCode, json);
                throw new HttpRequestException($"ThetaData Terminal bulk options failed: HTTP {response.StatusCode} - {json}");
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("response", out var respArray) && respArray.ValueKind == JsonValueKind.Array)
            {
                var headerList = new List<string>();
                if (root.TryGetProperty("header", out var hProp) && hProp.TryGetProperty("format", out var fArray))
                {
                    foreach (var h in fArray.EnumerateArray()) headerList.Add(h.GetString()?.ToLowerInvariant() ?? "");
                }

                // Standard ThetaData format: ["ms_of_day", "open", "high", "low", "close", "volume", "count", "date", "strike", "right", "expiration"]
                int openIdx = headerList.IndexOf("open");
                int highIdx = headerList.IndexOf("high");
                int lowIdx = headerList.IndexOf("low");
                int closeIdx = headerList.IndexOf("close");
                int volIdx = headerList.IndexOf("volume");
                int dateIdx = headerList.IndexOf("date");
                int strikeIdx = headerList.IndexOf("strike");
                int rightIdx = headerList.IndexOf("right");
                int expIdx = headerList.IndexOf("expiration");

                if (openIdx < 0) openIdx = 1;
                if (highIdx < 0) highIdx = 2;
                if (lowIdx < 0) lowIdx = 3;
                if (closeIdx < 0) closeIdx = 4;
                if (volIdx < 0) volIdx = 5;
                if (dateIdx < 0) dateIdx = 7;
                if (strikeIdx < 0) strikeIdx = 8;
                if (rightIdx < 0) rightIdx = 9;
                if (expIdx < 0) expIdx = 10;

                foreach (var row in respArray.EnumerateArray())
                {
                    if (row.ValueKind != JsonValueKind.Array) continue;
                    var r = row.EnumerateArray().ToArray();
                    if (r.Length <= Math.Max(expIdx, rightIdx)) continue;

                    int intDate = r[dateIdx].GetInt32();
                    var snapDate = new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);

                    int intExp = r[expIdx].GetInt32();
                    var expDate = new DateOnly(intExp / 10000, (intExp % 10000) / 100, intExp % 100);

                    var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                    if (dte < 0) continue;

                    // ThetaData strikes are typically represented in mills (e.g. 225000 = $225.00) or standard dollar format
                    decimal rawStrike = r[strikeIdx].GetDecimal();
                    decimal strike = rawStrike > 1000 ? rawStrike / 1000m : rawStrike;

                    var rightStr = r[rightIdx].GetString()?.ToUpperInvariant() ?? "C";
                    var side = rightStr.StartsWith("P") ? OptionSide.Put : OptionSide.Call;

                    decimal close = r[closeIdx].GetDecimal();
                    decimal low = r[lowIdx].GetDecimal();
                    decimal high = r[highIdx].GetDecimal();
                    long vol = r[volIdx].GetInt64();

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch bulk options from ThetaData Terminal for {Symbol}", symbol);
            throw new InvalidOperationException($"ThetaData Terminal unavailable or error for {symbol}: {ex.Message}. Please verify that the ThetaData Terminal application is active and running at {_httpClient.BaseAddress}.", ex);
        }

        return snapshots;
    }
}
