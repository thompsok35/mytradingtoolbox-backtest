using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Utils;
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
    private readonly ILogger<TradierClient> _logger;

    public TradierClient(HttpClient httpClient, IOptions<MarketDataSettings> settings, ILogger<TradierClient> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_settings.TradierBaseUrl))
        {
            _httpClient.BaseAddress = new Uri(_settings.TradierBaseUrl.TrimEnd('/') + "/");
        }
        
        if (!string.IsNullOrWhiteSpace(_settings.TradierApiToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _settings.TradierApiToken);
        }
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<(HistoricalStockCandle? Candle, List<HistoricalOptionSnapshot> Snapshots)> FetchDailyEodAsync(string symbol, DateOnly? date = null, CancellationToken ct = default)
    {
        var targetDate = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        symbol = symbol.Trim().ToUpperInvariant();

        // If no token is provided and simulation is enabled, generate high-fidelity market data
        if (string.IsNullOrWhiteSpace(_settings.TradierApiToken) && _settings.UseSimulatedDataIfNoToken)
        {
            _logger.LogInformation("No Tradier token configured; generating realistic EOD data for {Symbol} on {Date}", symbol, targetDate);
            return GenerateSimulatedEodData(symbol, targetDate);
        }

        try
        {
            // 1. Fetch Stock Quote
            var quoteResponse = await _httpClient.GetAsync($"markets/quotes?symbols={symbol}", ct);
            quoteResponse.EnsureSuccessStatusCode();
            var quoteJson = await quoteResponse.Content.ReadAsStringAsync(ct);
            
            decimal underlyingPrice = 100m;
            decimal open = 100m, high = 100m, low = 100m, close = 100m;
            long volume = 1000000;

            using (var doc = JsonDocument.Parse(quoteJson))
            {
                var quotes = doc.RootElement.GetProperty("quotes").GetProperty("quote");
                var quoteElem = quotes.ValueKind == JsonValueKind.Array ? quotes[0] : quotes;
                
                close = quoteElem.TryGetProperty("last", out var lastProp) && lastProp.ValueKind == JsonValueKind.Number ? lastProp.GetDecimal() :
                        quoteElem.TryGetProperty("close", out var closeProp) && closeProp.ValueKind == JsonValueKind.Number ? closeProp.GetDecimal() : 100m;
                open = quoteElem.TryGetProperty("open", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetDecimal() : close;
                high = quoteElem.TryGetProperty("high", out var h) && h.ValueKind == JsonValueKind.Number ? h.GetDecimal() : close * 1.01m;
                low = quoteElem.TryGetProperty("low", out var l) && l.ValueKind == JsonValueKind.Number ? l.GetDecimal() : close * 0.99m;
                volume = quoteElem.TryGetProperty("volume", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 1000000;
                underlyingPrice = close;
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

            // 2. Fetch Options Chain with Greeks
            var chainResponse = await _httpClient.GetAsync($"markets/options/chains?symbol={symbol}&greeks=true", ct);
            chainResponse.EnsureSuccessStatusCode();
            var chainJson = await chainResponse.Content.ReadAsStringAsync(ct);

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

                        var bid = opt.TryGetProperty("bid", out var b) ? b.GetDecimal() : 0m;
                        var ask = opt.TryGetProperty("ask", out var a) ? a.GetDecimal() : 0m;
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
                            DataSource = DataSource.TradierEOD
                        });
                    }
                }
            }

            return (candle, snapshots);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tradier API request failed for {Symbol} on {Date}. Falling back to simulation if enabled.", symbol, targetDate);
            if (_settings.UseSimulatedDataIfNoToken)
            {
                return GenerateSimulatedEodData(symbol, targetDate);
            }
            throw;
        }
    }

    public async Task<List<HistoricalStockCandle>> FetchHistoricalStockCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(_settings.TradierApiToken) && _settings.UseSimulatedDataIfNoToken)
        {
            return GenerateSimulatedHistoricalCandles(symbol, from, to);
        }

        try
        {
            var url = $"markets/history?symbol={symbol}&interval=daily&start={from:yyyy-MM-dd}&end={to:yyyy-MM-dd}";
            var response = await _httpClient.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(ct);

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch stock candles for {Symbol} via Tradier API; falling back to simulated history.", symbol);
            if (_settings.UseSimulatedDataIfNoToken)
            {
                return GenerateSimulatedHistoricalCandles(symbol, from, to);
            }
            throw;
        }
    }

    public static (HistoricalStockCandle Candle, List<HistoricalOptionSnapshot> Snapshots) GenerateSimulatedEodData(string symbol, DateOnly date, decimal? basePrice = null)
    {
        // Deterministic price based on symbol hash and date to make tests & demos repeatable
        var hash = Math.Abs(symbol.GetHashCode()) % 200 + 100;
        var dayOffset = date.DayNumber % 365;
        var trend = (decimal)Math.Sin(dayOffset * 0.05) * 15m;
        var closePrice = basePrice ?? (hash + trend);
        closePrice = Math.Round(closePrice, 2);

        var candle = new HistoricalStockCandle
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            Date = date,
            Open = Math.Round(closePrice * 0.995m, 2),
            High = Math.Round(closePrice * 1.015m, 2),
            Low = Math.Round(closePrice * 0.985m, 2),
            Close = closePrice,
            Volume = 4500000 + (dayOffset * 10000),
            Vwap = closePrice,
            DataSource = DataSource.Synthetic
        };

        var snapshots = new List<HistoricalOptionSnapshot>();

        // Generate DTE cycles: 7, 14, 21, 30, 45, 60, 90, 180
        var dteList = new[] { 7, 14, 21, 30, 45, 60, 90, 180 };
        var strikeSpacing = closePrice > 200 ? 5m : closePrice > 50 ? 2.5m : 1m;
        var centerStrike = Math.Round(closePrice / strikeSpacing) * strikeSpacing;

        foreach (var dte in dteList)
        {
            var expDate = date.AddDays(dte);
            var t = (double)dte / 365.0;
            var iv = 0.28m + ((decimal)Math.Cos(dte * 0.1) * 0.04m); // ~24% - 32% IV

            // Generate strikes from -20% to +20% around underlying price
            for (int i = -10; i <= 10; i++)
            {
                var strike = centerStrike + (i * strikeSpacing);
                if (strike <= 0) continue;

                // Black-Scholes analytical approximation for Call & Put Greeks and Price
                var s = (double)closePrice;
                var k = (double)strike;
                var sigma = (double)iv;
                var r = 0.045; // 4.5% risk free rate

                var d1 = (Math.Log(s / k) + (r + 0.5 * sigma * sigma) * t) / (sigma * Math.Sqrt(t));
                var d2 = d1 - sigma * Math.Sqrt(t);

                var callDelta = (decimal)CumulativeNormal(d1);
                var putDelta = callDelta - 1m;
                var gamma = (decimal)(NormalPdf(d1) / (s * sigma * Math.Sqrt(t)));
                var vega = (decimal)(s * NormalPdf(d1) * Math.Sqrt(t) / 100.0);
                var thetaCall = (decimal)(-(s * NormalPdf(d1) * sigma) / (2 * Math.Sqrt(t)) - r * k * Math.Exp(-r * t) * CumulativeNormal(d2)) / 365m;
                var thetaPut = (decimal)(-(s * NormalPdf(d1) * sigma) / (2 * Math.Sqrt(t)) + r * k * Math.Exp(-r * t) * CumulativeNormal(-d2)) / 365m;

                // Theoretical Prices
                var callPriceRaw = (decimal)(s * CumulativeNormal(d1) - k * Math.Exp(-r * t) * CumulativeNormal(d2));
                var putPriceRaw = (decimal)(k * Math.Exp(-r * t) * CumulativeNormal(-d2) - s * CumulativeNormal(-d1));

                var callMid = Math.Max(0.01m, Math.Round(callPriceRaw, 2));
                var putMid = Math.Max(0.01m, Math.Round(putPriceRaw, 2));

                var spreadHalf = Math.Max(0.02m, Math.Round(callMid * 0.02m, 2));

                // Call Snapshot
                var callSymbol = OCCParser.Format(symbol, expDate, OptionSide.Call, strike);
                snapshots.Add(new HistoricalOptionSnapshot
                {
                    Id = Guid.NewGuid(),
                    UnderlyingSymbol = symbol,
                    SnapshotDate = date,
                    OptionSymbol = callSymbol,
                    ExpirationDate = expDate,
                    DTE = dte,
                    Strike = strike,
                    Side = OptionSide.Call,
                    Bid = Math.Max(0.01m, callMid - spreadHalf),
                    Ask = callMid + spreadHalf,
                    Mid = callMid,
                    Last = callMid,
                    Delta = Math.Round(callDelta, 4),
                    Gamma = Math.Round(gamma, 5),
                    Theta = Math.Round(thetaCall, 4),
                    Vega = Math.Round(vega, 4),
                    Rho = 0.05m,
                    ImpliedVolatility = Math.Round(iv, 4),
                    UnderlyingPrice = closePrice,
                    Volume = 250 + Math.Abs(i) * 15,
                    OpenInterest = 1200 + Math.Abs(i) * 45,
                    DataSource = DataSource.Synthetic
                });

                // Put Snapshot
                var putSymbol = OCCParser.Format(symbol, expDate, OptionSide.Put, strike);
                snapshots.Add(new HistoricalOptionSnapshot
                {
                    Id = Guid.NewGuid(),
                    UnderlyingSymbol = symbol,
                    SnapshotDate = date,
                    OptionSymbol = putSymbol,
                    ExpirationDate = expDate,
                    DTE = dte,
                    Strike = strike,
                    Side = OptionSide.Put,
                    Bid = Math.Max(0.01m, putMid - spreadHalf),
                    Ask = putMid + spreadHalf,
                    Mid = putMid,
                    Last = putMid,
                    Delta = Math.Round(putDelta, 4),
                    Gamma = Math.Round(gamma, 5),
                    Theta = Math.Round(thetaPut, 4),
                    Vega = Math.Round(vega, 4),
                    Rho = -0.05m,
                    ImpliedVolatility = Math.Round(iv, 4),
                    UnderlyingPrice = closePrice,
                    Volume = 180 + Math.Abs(i) * 10,
                    OpenInterest = 950 + Math.Abs(i) * 30,
                    DataSource = DataSource.Synthetic
                });
            }
        }

        return (candle, snapshots);
    }

    public static List<HistoricalStockCandle> GenerateSimulatedHistoricalCandles(string symbol, DateOnly from, DateOnly to)
    {
        var candles = new List<HistoricalStockCandle>();
        var current = from;
        var hash = Math.Abs(symbol.GetHashCode()) % 150 + 120;

        while (current <= to)
        {
            if (current.DayOfWeek != DayOfWeek.Saturday && current.DayOfWeek != DayOfWeek.Sunday)
            {
                var dayOffset = current.DayNumber % 365;
                var trend = (decimal)Math.Sin(dayOffset * 0.04) * 20m + ((decimal)(current.DayNumber - from.DayNumber) * 0.05m);
                var close = Math.Round(hash + trend, 2);

                candles.Add(new HistoricalStockCandle
                {
                    Id = Guid.NewGuid(),
                    Symbol = symbol,
                    Date = current,
                    Open = Math.Round(close * 0.994m, 2),
                    High = Math.Round(close * 1.012m, 2),
                    Low = Math.Round(close * 0.988m, 2),
                    Close = close,
                    Volume = 3500000 + (dayOffset * 8000),
                    Vwap = close,
                    DataSource = DataSource.Synthetic
                });
            }
            current = current.AddDays(1);
        }

        return candles;
    }

    private static double CumulativeNormal(double x)
    {
        const double b1 = 0.319381530;
        const double b2 = -0.356563782;
        const double b3 = 1.781477937;
        const double b4 = -1.821255978;
        const double b5 = 1.330274429;
        const double p = 0.2316419;
        const double c = 0.39894228;

        if (x >= 0.0)
        {
            double t = 1.0 / (1.0 + p * x);
            return (1.0 - c * Math.Exp(-x * x / 2.0) * t *
                (t * (t * (t * (t * b5 + b4) + b3) + b2) + b1));
        }
        else
        {
            double t = 1.0 / (1.0 - p * x);
            return (c * Math.Exp(-x * x / 2.0) * t *
                (t * (t * (t * (t * b5 + b4) + b3) + b2) + b1));
        }
    }

    private static double NormalPdf(double x)
    {
        return Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);
    }
}
