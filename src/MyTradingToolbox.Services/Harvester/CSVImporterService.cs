using System.Globalization;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Utils;

namespace MyTradingToolbox.Services.Harvester;

public interface ICSVImporterService
{
    Task<(int SnapshotsInserted, int CandlesInserted, string Report)> ImportCsvAsync(Stream stream, string? fallbackSymbol = null, CancellationToken ct = default);
}

public class CSVImporterService : ICSVImporterService
{
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly IStockCandleRepository _candleRepo;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly ILogger<CSVImporterService> _logger;

    public CSVImporterService(
        IOptionSnapshotRepository optionRepo,
        IStockCandleRepository candleRepo,
        IWatchlistRepository watchlistRepo,
        ILogger<CSVImporterService> logger)
    {
        _optionRepo = optionRepo;
        _candleRepo = candleRepo;
        _watchlistRepo = watchlistRepo;
        _logger = logger;
    }

    public async Task<(int SnapshotsInserted, int CandlesInserted, string Report)> ImportCsvAsync(Stream stream, string? fallbackSymbol = null, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => Regex.Replace(args.Header, @"[\s_\-\.\/]", "").ToLowerInvariant(),
            MissingFieldFound = null,
            BadDataFound = null
        });

        await csv.ReadAsync();
        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        var cleanHeaders = headers.Select(h => Regex.Replace(h, @"[\s_\-\.\/]", "").ToLowerInvariant()).ToList();

        var snapshots = new List<HistoricalOptionSnapshot>();
        var candleMap = new Dictionary<string, HistoricalStockCandle>();

        int rowIndex = 0;
        int parsedRows = 0;

        while (await csv.ReadAsync())
        {
            rowIndex++;
            try
            {
                var occ = GetString(csv, cleanHeaders, "optionsymbol", "occ", "contract", "symbol");
                var root = GetString(csv, cleanHeaders, "underlyingsymbol", "root", "ticker", "underlying") ?? fallbackSymbol;
                var dateStr = GetString(csv, cleanHeaders, "snapshotdate", "date", "tradedate", "quotedate");
                var expStr = GetString(csv, cleanHeaders, "expirationdate", "expiration", "expiry", "expiredate");
                var typeStr = GetString(csv, cleanHeaders, "side", "type", "callput", "optiontype");
                var strikeVal = GetDecimal(csv, cleanHeaders, "strike", "strikeprice");
                var bid = GetDecimal(csv, cleanHeaders, "bid") ?? 0m;
                var ask = GetDecimal(csv, cleanHeaders, "ask") ?? 0m;
                var last = GetDecimal(csv, cleanHeaders, "last", "close", "price") ?? 0m;
                var underPrice = GetDecimal(csv, cleanHeaders, "underlyingprice", "spotprice", "stockprice", "spot") ?? 100m;
                
                var delta = GetDecimal(csv, cleanHeaders, "delta");
                var gamma = GetDecimal(csv, cleanHeaders, "gamma");
                var theta = GetDecimal(csv, cleanHeaders, "theta");
                var vega = GetDecimal(csv, cleanHeaders, "vega");
                var rho = GetDecimal(csv, cleanHeaders, "rho");
                var iv = GetDecimal(csv, cleanHeaders, "impliedvolatility", "iv", "volatility", "midiv");
                var volume = GetLong(csv, cleanHeaders, "volume", "vol") ?? 0;
                var oi = GetLong(csv, cleanHeaders, "openinterest", "oi") ?? 0;

                // Parse Snapshot Date
                DateOnly snapshotDate = DateOnly.FromDateTime(DateTime.UtcNow);
                if (!string.IsNullOrWhiteSpace(dateStr))
                {
                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                        snapshotDate = DateOnly.FromDateTime(dt);
                }

                // If OCC symbol is present, extract details if missing
                if (!string.IsNullOrWhiteSpace(occ) && OCCParser.TryParse(occ, out var parsedRoot, out var parsedExp, out var parsedSide, out var parsedStrike))
                {
                    if (string.IsNullOrWhiteSpace(root)) root = parsedRoot;
                    if (string.IsNullOrWhiteSpace(expStr)) expStr = parsedExp.ToString("yyyy-MM-dd");
                    if (strikeVal == null) strikeVal = parsedStrike;
                    if (string.IsNullOrWhiteSpace(typeStr)) typeStr = parsedSide.ToString();
                }

                if (string.IsNullOrWhiteSpace(root))
                {
                    continue;
                }

                root = root.Trim().ToUpperInvariant();
                DateOnly expDate = snapshotDate.AddDays(30);
                if (!string.IsNullOrWhiteSpace(expStr) && DateTime.TryParse(expStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var expDt))
                {
                    expDate = DateOnly.FromDateTime(expDt);
                }

                var side = OptionSide.Call;
                if (!string.IsNullOrWhiteSpace(typeStr) && (typeStr.StartsWith("p", StringComparison.OrdinalIgnoreCase) || typeStr.Equals("put", StringComparison.OrdinalIgnoreCase)))
                {
                    side = OptionSide.Put;
                }

                var strike = strikeVal ?? 100m;
                var dte = (expDate.ToDateTime(TimeOnly.MinValue) - snapshotDate.ToDateTime(TimeOnly.MinValue)).Days;
                if (dte < 0) dte = 0;

                var optSym = !string.IsNullOrWhiteSpace(occ) && occ.Length >= 15 
                    ? occ 
                    : OCCParser.Format(root, expDate, side, strike);

                var snapshot = new HistoricalOptionSnapshot
                {
                    Id = Guid.NewGuid(),
                    UnderlyingSymbol = root,
                    SnapshotDate = snapshotDate,
                    OptionSymbol = optSym,
                    ExpirationDate = expDate,
                    DTE = dte,
                    Strike = strike,
                    Side = side,
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
                    UnderlyingPrice = underPrice,
                    Volume = volume,
                    OpenInterest = oi,
                    DataSource = DataSource.CSVImport
                };

                snapshots.Add(snapshot);
                parsedRows++;

                // Track candle
                var candleKey = $"{root}_{snapshotDate:yyyyMMdd}";
                if (!candleMap.ContainsKey(candleKey))
                {
                    candleMap[candleKey] = new HistoricalStockCandle
                    {
                        Id = Guid.NewGuid(),
                        Symbol = root,
                        Date = snapshotDate,
                        Open = underPrice,
                        High = underPrice,
                        Low = underPrice,
                        Close = underPrice,
                        Volume = 1000000,
                        Vwap = underPrice,
                        DataSource = DataSource.CSVImport
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse CSV row {RowIndex}", rowIndex);
            }
        }

        int snapInserted = 0;
        int candlesInserted = 0;

        if (snapshots.Count > 0)
        {
            snapInserted = await _optionRepo.UpsertSnapshotsAsync(snapshots, ct);
            candlesInserted = await _candleRepo.UpsertCandlesAsync(candleMap.Values, ct);

            // Update watchlist symbols
            var symbols = snapshots.Select(s => s.UnderlyingSymbol).Distinct();
            foreach (var sym in symbols)
            {
                await _watchlistRepo.AddOrUpdateAsync(new WatchlistSymbol
                {
                    Symbol = sym,
                    AssetType = AssetType.Equity,
                    IsActiveHarvesting = true
                }, ct);
                await _watchlistRepo.UpdateCoverageStatsAsync(sym, ct);
            }
        }

        var report = $"CSV Ingestion complete: Parsed {parsedRows} option rows from {rowIndex} lines. Upserted {snapInserted} snapshot records and {candlesInserted} daily stock candles across {candleMap.Count} dates.";
        _logger.LogInformation(report);

        return (snapInserted, candlesInserted, report);
    }

    private static string? GetString(CsvReader csv, List<string> headers, params string[] fieldNames)
    {
        foreach (var name in fieldNames)
        {
            var idx = headers.IndexOf(name);
            if (idx >= 0)
            {
                var val = csv.GetField(idx);
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
        }
        return null;
    }

    private static decimal? GetDecimal(CsvReader csv, List<string> headers, params string[] fieldNames)
    {
        var str = GetString(csv, headers, fieldNames);
        if (str == null) return null;
        str = str.Replace("$", "").Replace(",", "").Trim();
        if (decimal.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            return val;
        return null;
    }

    private static long? GetLong(CsvReader csv, List<string> headers, params string[] fieldNames)
    {
        var str = GetString(csv, headers, fieldNames);
        if (str == null) return null;
        str = str.Replace(",", "").Trim();
        if (long.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            return val;
        return null;
    }
}
