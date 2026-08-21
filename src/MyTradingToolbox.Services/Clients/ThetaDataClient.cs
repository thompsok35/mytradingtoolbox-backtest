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
        _httpClient.Timeout = TimeSpan.FromSeconds(120);
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
            else
            {
                // Parse CSV
                var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines.Skip(1))
                {
                    var val = line.Trim();
                    if (int.TryParse(val, out var intDate) && intDate > 19000000)
                    {
                        expirations.Add(new DateOnly(intDate / 10000, (intDate % 10000) / 100, intDate % 100));
                    }
                    else if (DateOnly.TryParse(val, out var d))
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
                    var items = root.TryGetProperty("response", out var rProp) ? rProp : root;

                    if (items.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object)
                            {
                                DateOnly cDate = chunkStart;
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
                else
                {
                    // Parse CSV
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 1)
                    {
                        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
                        int dateIdx = headers.IndexOf("date");
                        int openIdx = headers.IndexOf("open");
                        int highIdx = headers.IndexOf("high");
                        int lowIdx = headers.IndexOf("low");
                        int closeIdx = headers.IndexOf("close");
                        int volIdx = headers.IndexOf("volume");

                        foreach (var line in lines.Skip(1))
                        {
                            var parts = line.Split(',');
                            if (parts.Length < headers.Count) continue;

                            DateOnly cDate = chunkStart;
                            if (dateIdx >= 0 && int.TryParse(parts[dateIdx], out var idt))
                            {
                                cDate = new DateOnly(idt / 10000, (idt % 10000) / 100, idt % 100);
                            }

                            decimal open = openIdx >= 0 && decimal.TryParse(parts[openIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var oVal) ? oVal : 0m;
                            decimal high = highIdx >= 0 && decimal.TryParse(parts[highIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal) ? hVal : 0m;
                            decimal low = lowIdx >= 0 && decimal.TryParse(parts[lowIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal) ? lVal : 0m;
                            decimal close = closeIdx >= 0 && decimal.TryParse(parts[closeIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var cVal) ? cVal : 0m;
                            long vol = volIdx >= 0 && long.TryParse(parts[volIdx], out var vVal) ? vVal : 0;

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
            var chunkEnd = chunkStart.AddDays(180) > effectiveTo ? effectiveTo : chunkStart.AddDays(180);
            var startStr = $"{chunkStart:yyyyMMdd}";
            var endStr = $"{chunkEnd:yyyyMMdd}";

            // Query with format=json
            var url = $"option/history/eod?symbol={symbol}&expiration=*&start_date={startStr}&end_date={endStr}&format=json";

            try
            {
                var response = await _httpClient.GetAsync(url, ct);
                var content = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("ThetaData v3 option/history/eod failed: HTTP {Status}: {Body}", response.StatusCode, content);
                    throw new HttpRequestException($"ThetaData Terminal v3 options failed: HTTP {response.StatusCode} - {content}");
                }

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

                            // Handle ThetaData v3 Contract object
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

                            // If item has a nested 'data' array (one per historical day)
                            if (item.TryGetProperty("data", out var dataArr) && dataArr.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var dItem in dataArr.EnumerateArray())
                                {
                                    if (dItem.ValueKind != JsonValueKind.Object) continue;

                                    DateOnly snapDate = chunkStart;
                                    if (dItem.TryGetProperty("date", out var dElem))
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
                                    else if (dItem.TryGetProperty("created", out var crElem) && DateTime.TryParse(crElem.GetString(), out var crDt))
                                    {
                                        snapDate = DateOnly.FromDateTime(crDt);
                                    }
                                    else if (dItem.TryGetProperty("last_trade", out var ltElem) && DateTime.TryParse(ltElem.GetString(), out var ltDt))
                                    {
                                        snapDate = DateOnly.FromDateTime(ltDt);
                                    }

                                    var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                                    if (dte < 0) continue;

                                    decimal close = dItem.TryGetProperty("close", out var cElem) ? cElem.GetDecimal() : 0m;
                                    decimal low = dItem.TryGetProperty("low", out var lElem) ? lElem.GetDecimal() : 0m;
                                    decimal high = dItem.TryGetProperty("high", out var hElem) ? hElem.GetDecimal() : 0m;
                                    decimal bid = dItem.TryGetProperty("bid", out var bElem) ? bElem.GetDecimal() : 0m;
                                    decimal ask = dItem.TryGetProperty("ask", out var aElem) ? aElem.GetDecimal() : 0m;
                                    long vol = dItem.TryGetProperty("volume", out var vElem) && vElem.ValueKind == JsonValueKind.Number ? vElem.GetInt64() : 0;

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
                                        OpenInterest = 0,
                                        DataSource = DataSource.ThetaData
                                    });
                                }
                            }
                            else
                            {
                                // Flat contract item
                                DateOnly snapDate = chunkStart;
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

                                var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                                if (dte < 0) continue;

                                decimal close = item.TryGetProperty("close", out var cElem) ? cElem.GetDecimal() : 0m;
                                decimal low = item.TryGetProperty("low", out var lElem) ? lElem.GetDecimal() : 0m;
                                decimal high = item.TryGetProperty("high", out var hElem) ? hElem.GetDecimal() : 0m;
                                decimal bid = item.TryGetProperty("bid", out var bElem) ? bElem.GetDecimal() : Math.Max(0.01m, low);
                                decimal ask = item.TryGetProperty("ask", out var aElem) ? aElem.GetDecimal() : Math.Max(bid, high > 0 ? high : close);
                                long vol = item.TryGetProperty("volume", out var vElem) && vElem.ValueKind == JsonValueKind.Number ? vElem.GetInt64() : 0;
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
                else
                {
                    // Parse CSV lines
                    var lines = content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length > 1)
                    {
                        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
                        int dateIdx = headers.IndexOf("date");
                        int expIdx = headers.IndexOf("expiration");
                        int strikeIdx = headers.IndexOf("strike");
                        int rightIdx = headers.IndexOf("right");
                        int closeIdx = headers.IndexOf("close");
                        int lowIdx = headers.IndexOf("low");
                        int highIdx = headers.IndexOf("high");
                        int volIdx = headers.IndexOf("volume");

                        foreach (var line in lines.Skip(1))
                        {
                            var parts = line.Split(',');
                            if (parts.Length < headers.Count) continue;

                            DateOnly snapDate = chunkStart;
                            if (dateIdx >= 0 && int.TryParse(parts[dateIdx], out var idt))
                            {
                                snapDate = new DateOnly(idt / 10000, (idt % 10000) / 100, idt % 100);
                            }

                            DateOnly expDate = snapDate.AddDays(7);
                            if (expIdx >= 0 && int.TryParse(parts[expIdx], out var iexp))
                            {
                                expDate = new DateOnly(iexp / 10000, (iexp % 10000) / 100, iexp % 100);
                            }

                            var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapDate.ToDateTime(TimeOnly.MinValue)).Days;
                            if (dte < 0) continue;

                            decimal strike = strikeIdx >= 0 && decimal.TryParse(parts[strikeIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var sVal) ? sVal : 0m;
                            if (strike > 1000) strike /= 1000m;

                            var rightStr = rightIdx >= 0 ? parts[rightIdx].Trim().ToUpperInvariant() : "C";
                            var side = rightStr.StartsWith("P") ? OptionSide.Put : OptionSide.Call;

                            decimal close = closeIdx >= 0 && decimal.TryParse(parts[closeIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var cVal) ? cVal : 0m;
                            decimal low = lowIdx >= 0 && decimal.TryParse(parts[lowIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var lVal) ? lVal : 0m;
                            decimal high = highIdx >= 0 && decimal.TryParse(parts[highIdx], NumberStyles.Any, CultureInfo.InvariantCulture, out var hVal) ? hVal : 0m;
                            long vol = volIdx >= 0 && long.TryParse(parts[volIdx], out var vVal) ? vVal : 0;

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

            chunkStart = chunkEnd.AddDays(1);
        }

        return snapshots;
    }
}
