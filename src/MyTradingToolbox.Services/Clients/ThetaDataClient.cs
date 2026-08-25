using System.Globalization;
using System.Net.Http.Headers;
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
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
        _httpClient.Timeout = TimeSpan.FromSeconds(300);
    }

    public async Task<bool> TestTerminalConnectionAsync(CancellationToken ct = default)
    {
        try
        {
            var res = await _httpClient.GetAsync("stock/list/symbols?format=json", ct);
            return res.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ThetaData Terminal connection test failed at {BaseAddress}", _httpClient.BaseAddress);
            return false;
        }
    }

    private static int FindHeaderIndex(List<string> headers, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            int idx = headers.IndexOf(candidate);
            if (idx >= 0) return idx;
        }
        return -1;
    }

    private static DateOnly ParseDate(string? raw, DateOnly fallback)
    {
        if (string.IsNullOrWhiteSpace(raw)) return fallback;
        raw = raw.Trim().Trim('"', '\'');

        // 1. Integer YYYYMMDD (e.g. 20250102)
        if (int.TryParse(raw, out var intDate) && intDate >= 19900101 && intDate <= 21001231)
        {
            return new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);
        }

        // 2. Standard ISO / culture formats (e.g. "2025-01-02", "2025/01/02", "1/2/2025")
        if (DateOnly.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
            return d1;
        if (DateOnly.TryParse(raw, out var d2))
            return d2;

        // 3. DateTime formats with timestamps (e.g. "2025-01-02T16:00:00Z", "2025-01-02 16:00:00")
        if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt1))
            return DateOnly.FromDateTime(dt1);
        if (DateTime.TryParse(raw, out var dt2))
            return DateOnly.FromDateTime(dt2);

        // 4. Unix timestamp (ms or s)
        if (long.TryParse(raw, out var unixTs))
        {
            if (unixTs > 1_000_000_000_000)
                return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(unixTs).UtcDateTime);
            if (unixTs > 1_000_000_000)
                return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime);
        }

        return fallback;
    }

    private static DateOnly ParseJsonDate(JsonElement elem, DateOnly fallback)
    {
        if (elem.ValueKind == JsonValueKind.Number)
        {
            if (elem.TryGetInt32(out var intDate) && intDate >= 19900101 && intDate <= 21001231)
            {
                return new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100);
            }
            if (elem.TryGetInt64(out var unixTs))
            {
                if (unixTs > 1_000_000_000_000)
                    return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeMilliseconds(unixTs).UtcDateTime);
                if (unixTs > 1_000_000_000)
                    return DateOnly.FromDateTime(DateTimeOffset.FromUnixTimeSeconds(unixTs).UtcDateTime);
            }
        }
        else if (elem.ValueKind == JsonValueKind.String)
        {
            return ParseDate(elem.GetString(), fallback);
        }
        return fallback;
    }

    public async Task<List<DateOnly>> FetchAvailableExpirationsAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var expirations = new List<DateOnly>();

        try
        {
            var url = $"option/list/expirations?symbol={symbol}&format=json";
            var response = await _httpClient.GetAsync(url, ct);
            var content = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException($"ThetaData v3 option/list/expirations failed: HTTP {response.StatusCode} - {content}");
            }

            content = content.Trim();
            if (content.StartsWith("{") || content.StartsWith("["))
            {
                using var doc = JsonDocument.Parse(content);
                var root = doc.RootElement;

                var expArray = root.TryGetProperty("response", out var rProp) ? rProp : root;
                if (expArray.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in expArray.EnumerateArray())
                    {
                        var parsed = ParseJsonDate(item, default);
                        if (parsed != default)
                        {
                            expirations.Add(parsed);
                        }
                    }
                }
            }
            else
            {
                // Parse CSV
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1))
                {
                    var parsed = ParseDate(line, default);
                    if (parsed != default)
                    {
                        expirations.Add(parsed);
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

        var chunkStart = from;
        while (chunkStart <= to)
        {
            var chunkEnd = chunkStart.AddDays(180) > to ? to : chunkStart.AddDays(180);
            var startStr = $"{chunkStart:yyyyMMdd}";
            var endStr = $"{chunkEnd:yyyyMMdd}";
            var url = $"stock/history/eod?symbol={symbol}&start_date={startStr}&end_date={endStr}&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ThetaData v3 stock history failed: {Url} -> HTTP {Status}: {Body}", url, response.StatusCode, content);
                    throw new HttpRequestException($"ThetaData Terminal stock history failed: HTTP {response.StatusCode} - {content}");
                }

                content = content.Trim();
                if (content.StartsWith("{") || content.StartsWith("["))
                {
                    using var doc = JsonDocument.Parse(content);
                    var root = doc.RootElement;

                    JsonElement items = root;
                    if (root.TryGetProperty("data", out var dProp) && dProp.ValueKind == JsonValueKind.Array)
                    {
                        items = dProp;
                    }
                    else if (root.TryGetProperty("response", out var rProp) && rProp.ValueKind == JsonValueKind.Array)
                    {
                        items = rProp;
                    }

                    if (items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;

                            DateOnly cDate = chunkStart;
                            if (item.TryGetProperty("date", out var dElem))
                            {
                                cDate = ParseJsonDate(dElem, chunkStart);
                            }
                            else if (item.TryGetProperty("created", out var crElem))
                            {
                                cDate = ParseJsonDate(crElem, chunkStart);
                            }
                            else if (item.TryGetProperty("last_trade", out var ltElem))
                            {
                                cDate = ParseJsonDate(ltElem, chunkStart);
                            }

                            decimal open = item.TryGetProperty("open", out var o) ? o.GetDecimal() : 0m;
                            decimal high = item.TryGetProperty("high", out var h) ? h.GetDecimal() : 0m;
                            decimal low = item.TryGetProperty("low", out var l) ? l.GetDecimal() : 0m;
                            decimal close = item.TryGetProperty("close", out var c) ? c.GetDecimal() : 0m;
                            long vol = item.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;

                            if (open > 0 || high > 0 || low > 0 || close > 0)
                            {
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
                else
                {
                    // Parse CSV
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 1)
                    {
                        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
                        int dateIdx = FindHeaderIndex(headers, "date", "created", "trade_date", "tradedate", "timestamp", "datetime", "time", "record_date");
                        int openIdx = FindHeaderIndex(headers, "open", "first");
                        int highIdx = FindHeaderIndex(headers, "high", "max");
                        int lowIdx = FindHeaderIndex(headers, "low", "min");
                        int closeIdx = FindHeaderIndex(headers, "close", "last", "settle", "price");
                        int volIdx = FindHeaderIndex(headers, "volume", "vol", "trades", "count");

                        foreach (var line in lines.Skip(1))
                        {
                            var parts = line.Split(',');
                            if (parts.Length < headers.Count) continue;

                            DateOnly cDate = dateIdx >= 0 ? ParseDate(parts[dateIdx], chunkStart) : chunkStart;

                            decimal open = openIdx >= 0 && decimal.TryParse(parts[openIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var oVal) ? oVal : 0m;
                            decimal high = highIdx >= 0 && decimal.TryParse(parts[highIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal) ? hVal : 0m;
                            decimal low = lowIdx >= 0 && decimal.TryParse(parts[lowIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal) ? lVal : 0m;
                            decimal close = closeIdx >= 0 && decimal.TryParse(parts[closeIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cVal) ? cVal : 0m;
                            long vol = volIdx >= 0 && long.TryParse(parts[volIdx].Trim(), out var vVal) ? vVal : 0;

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

            chunkStart = chunkEnd.AddDays(1);
        }

        return candles;
    }

    public async Task<List<HistoricalOptionSnapshot>> FetchEodHistoricalRangeAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var effectiveTo = to >= today ? today.AddDays(-1) : to;
        if (effectiveTo < from) effectiveTo = from;

        _logger.LogInformation("Retrieving real historical option chains from ThetaData Terminal v3 for {Symbol} from {From} to {To}", symbol, from, effectiveTo);

        var snapshots = new List<HistoricalOptionSnapshot>();

        var chunkStart = from;
        while (chunkStart <= effectiveTo)
        {
            // Chunk by 7 days to prevent large streaming timeouts on heavily traded symbols like NVDA
            var chunkEnd = chunkStart.AddDays(7) > effectiveTo ? effectiveTo : chunkStart.AddDays(7);
            var startStr = $"{chunkStart:yyyyMMdd}";
            var endStr = $"{chunkEnd:yyyyMMdd}";

            // Query with format=csv for lightweight and fast payload streaming
            var url = $"option/history/eod?symbol={symbol}&expiration=*&start_date={startStr}&end_date={endStr}&format=csv";

            int chunkCount = 0;
            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("ThetaData v3 option/history/eod chunk {Start}-{End} returned HTTP {Status}: {Body}", startStr, endStr, response.StatusCode, content);
                }
                else
                {
                    content = content.Trim();
                    if (content.StartsWith("{") || content.StartsWith("["))
                    {
                        using var doc = JsonDocument.Parse(content);
                        var root = doc.RootElement;
                        var items = root.TryGetProperty("response", out var rProp) ? rProp : root;

                        if (items.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var item in items.EnumerateArray())
                            {
                                if (item.ValueKind != JsonValueKind.Object) continue;

                                JsonElement contractElem = item;
                                if (item.TryGetProperty("contract", out var cObj) && cObj.ValueKind == JsonValueKind.Object)
                                {
                                    contractElem = cObj;
                                }

                                var rightStr = contractElem.TryGetProperty("right", out var rElem) ? rElem.GetString()?.ToUpperInvariant() ?? "C" : "C";
                                var side = rightStr.StartsWith("P") ? OptionSide.Put : OptionSide.Call;

                                decimal strike = contractElem.TryGetProperty("strike", out var stk) ? stk.GetDecimal() : 0m;
                                if (strike > 1000) strike /= 1000m;

                                DateOnly expDate = chunkStart.AddDays(7);
                                if (contractElem.TryGetProperty("expiration", out var expElem))
                                {
                                    expDate = ParseJsonDate(expElem, expDate);
                                }

                                if (item.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                                {
                                    foreach (var dItem in dataArr.EnumerateArray())
                                    {
                                        if (dItem.ValueKind != JsonValueKind.Object) continue;

                                        DateOnly snapDate = chunkStart;
                                        if (dItem.TryGetProperty("date", out var dElem))
                                        {
                                            snapDate = ParseJsonDate(dElem, chunkStart);
                                        }
                                        else if (dItem.TryGetProperty("created", out var crElem))
                                        {
                                            snapDate = ParseJsonDate(crElem, chunkStart);
                                        }
                                        else if (dItem.TryGetProperty("last_trade", out var ltElem))
                                        {
                                            snapDate = ParseJsonDate(ltElem, chunkStart);
                                        }

                                        var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                                        if (dte < 0) continue;

                                        decimal close = dItem.TryGetProperty("close", out var cElem) ? cElem.GetDecimal() : 0m;
                                        decimal low = dItem.TryGetProperty("low", out var lElem) ? lElem.GetDecimal() : 0m;
                                        decimal high = dItem.TryGetProperty("high", out var hElem) ? hElem.GetDecimal() : 0m;
                                        decimal bid = dItem.TryGetProperty("bid", out var bElem) ? bElem.GetDecimal() : 0m;
                                        decimal ask = dItem.TryGetProperty("ask", out var aElem) ? aElem.GetDecimal() : 0m;
                                        long vol = dItem.TryGetProperty("volume", out var vElem) && vElem.ValueKind == JsonValueKind.Number ? vElem.GetInt64() : 0;
                                        long oi = dItem.TryGetProperty("open_interest", out var oiElem) && oiElem.ValueKind == JsonValueKind.Number ? oiElem.GetInt64() : 0;

                                        if (bid <= 0 && ask <= 0)
                                        {
                                            bid = Math.Max(0.01m, low);
                                            ask = Math.Max(bid, high > 0 ? high : close);
                                        }
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
                                            OpenInterest = oi,
                                            DataSource = DataSource.ThetaData
                                        });
                                        chunkCount++;
                                    }
                                }
                                else
                                {
                                    DateOnly snapDate = chunkStart;
                                    if (item.TryGetProperty("date", out var dElem))
                                    {
                                        snapDate = ParseJsonDate(dElem, chunkStart);
                                    }
                                    else if (item.TryGetProperty("created", out var crElem))
                                    {
                                        snapDate = ParseJsonDate(crElem, chunkStart);
                                    }

                                    var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                                    if (dte < 0) continue;

                                    decimal close = item.TryGetProperty("close", out var cElem) ? cElem.GetDecimal() : 0m;
                                    decimal low = item.TryGetProperty("low", out var lElem) ? lElem.GetDecimal() : 0m;
                                    decimal high = item.TryGetProperty("high", out var hElem) ? hElem.GetDecimal() : 0m;
                                    decimal bid = item.TryGetProperty("bid", out var bElem) ? bElem.GetDecimal() : Math.Max(0.01m, low);
                                    decimal ask = item.TryGetProperty("ask", out var aElem) ? aElem.GetDecimal() : Math.Max(bid, high > 0 ? high : close);
                                    long vol = item.TryGetProperty("volume", out var vElem) && vElem.ValueKind == JsonValueKind.Number ? vElem.GetInt64() : 0;
                                    long oi = item.TryGetProperty("open_interest", out var oiElem) && oiElem.ValueKind == JsonValueKind.Number ? oiElem.GetInt64() : 0;
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
                                        OpenInterest = oi,
                                        DataSource = DataSource.ThetaData
                                    });
                                    chunkCount++;
                                }
                            }
                        }
                    }
                    else
                    {
                        // Parse CSV lines
                        var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        if (lines.Length > 1)
                        {
                            var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
                            int dateIdx = FindHeaderIndex(headers, "date", "created", "trade_date", "tradedate", "timestamp", "datetime", "time", "record_date");
                            int expIdx = FindHeaderIndex(headers, "expiration", "exp", "expiry", "expiration_date", "expire_date", "exp_date");
                            int strikeIdx = FindHeaderIndex(headers, "strike", "stk", "strike_price", "strikeprice");
                            int rightIdx = FindHeaderIndex(headers, "right", "side", "call_put", "callput", "type", "option_type");
                            int closeIdx = FindHeaderIndex(headers, "close", "last", "settle", "settlement", "price");
                            int lowIdx = FindHeaderIndex(headers, "low", "min");
                            int highIdx = FindHeaderIndex(headers, "high", "max");
                            int volIdx = FindHeaderIndex(headers, "volume", "vol", "trades", "count");
                            int bidIdx = FindHeaderIndex(headers, "bid", "bid_price", "bidprice");
                            int askIdx = FindHeaderIndex(headers, "ask", "ask_price", "askprice");
                            int oiIdx = FindHeaderIndex(headers, "open_interest", "openinterest", "oi");
                            int undIdx = FindHeaderIndex(headers, "underlying_price", "underlyingprice", "underlying", "spot", "stock_price");
                            int symIdx = FindHeaderIndex(headers, "option_symbol", "optionsymbol", "occ_symbol", "contract", "symbol", "root");

                            foreach (var line in lines.Skip(1))
                            {
                                var parts = line.Split(',');
                                if (parts.Length < headers.Count) continue;

                                DateOnly snapDate = dateIdx >= 0 ? ParseDate(parts[dateIdx], chunkStart) : chunkStart;
                                DateOnly expDate;
                                OptionSide side = OptionSide.Call;
                                decimal strike = 0m;

                                if (symIdx >= 0 && OCCParser.TryParse(parts[symIdx].Trim(), out _, out var parsedExp, out var parsedSide, out var parsedStrike))
                                {
                                    expDate = parsedExp;
                                    side = parsedSide;
                                    strike = parsedStrike;
                                }
                                else
                                {
                                    expDate = expIdx >= 0 ? ParseDate(parts[expIdx], snapDate.AddDays(7)) : snapDate.AddDays(7);
                                    if (strikeIdx >= 0 && decimal.TryParse(parts[strikeIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var sVal))
                                    {
                                        strike = sVal > 1000 ? sVal / 1000m : sVal;
                                    }
                                    var rightStr = rightIdx >= 0 ? parts[rightIdx].Trim().ToUpperInvariant() : "C";
                                    side = rightStr.StartsWith("P") ? OptionSide.Put : OptionSide.Call;
                                }

                                var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                                if (dte < 0) continue;

                                decimal close = closeIdx >= 0 && decimal.TryParse(parts[closeIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var cVal) ? cVal : 0m;
                                decimal low = lowIdx >= 0 && decimal.TryParse(parts[lowIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal) ? lVal : 0m;
                                decimal high = highIdx >= 0 && decimal.TryParse(parts[highIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal) ? hVal : 0m;
                                long vol = volIdx >= 0 && long.TryParse(parts[volIdx].Trim(), out var vVal) ? vVal : 0;
                                long oi = oiIdx >= 0 && long.TryParse(parts[oiIdx].Trim(), out var oiVal) ? oiVal : 0;
                                decimal undPrice = undIdx >= 0 && decimal.TryParse(parts[undIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var upVal) ? upVal : 0m;

                                decimal bid = bidIdx >= 0 && decimal.TryParse(parts[bidIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var bVal) ? bVal : Math.Max(0.01m, low);
                                decimal ask = askIdx >= 0 && decimal.TryParse(parts[askIdx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var aVal) ? aVal : Math.Max(bid, high > 0 ? high : close);

                                if (bid <= 0 && ask <= 0)
                                {
                                    bid = Math.Max(0.01m, low);
                                    ask = Math.Max(bid, high > 0 ? high : close);
                                }
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
                                    UnderlyingPrice = undPrice,
                                    Volume = vol,
                                    OpenInterest = oi,
                                    DataSource = DataSource.ThetaData
                                });
                                chunkCount++;
                            }
                        }
                    }

                    _logger.LogInformation("ThetaData v3 fetched {ChunkCount} options for {Symbol} chunk {Start} to {End} (Total: {Total})", chunkCount, symbol, startStr, endStr, snapshots.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch bulk options from ThetaData Terminal for {Symbol} chunk {Start} to {End}", symbol, startStr, endStr);
                throw new InvalidOperationException($"ThetaData Terminal v3 error for {symbol}: {ex.Message}. Make sure ThetaTerminal is running on port 25503 and logged in.", ex);
            }

            chunkStart = chunkEnd.AddDays(1);
        }

        return snapshots;
    }
}
