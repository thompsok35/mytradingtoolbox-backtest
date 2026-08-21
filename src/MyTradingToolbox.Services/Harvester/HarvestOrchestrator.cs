using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Services.Clients;
using MyTradingToolbox.Services.Integrity;

namespace MyTradingToolbox.Services.Harvester;

public interface IHarvestOrchestrator
{
    Task<DataHarvestJob> RunDailyHarvestAsync(CancellationToken ct = default);
    Task<DataHarvestJob> TriggerSeedAsync(string symbol, JobType source, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<int> RunCatchupBackfillAsync(CancellationToken ct = default);
}

public class HarvestOrchestrator : IHarvestOrchestrator
{
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly IStockCandleRepository _candleRepo;
    private readonly IHarvestJobRepository _jobRepo;
    private readonly ITradierClient _tradierClient;
    private readonly IThetaDataClient _thetaDataClient;
    private readonly IMarketDataClient _marketDataClient;
    private readonly ILogger<HarvestOrchestrator> _logger;

    public HarvestOrchestrator(
        IWatchlistRepository watchlistRepo,
        IOptionSnapshotRepository optionRepo,
        IStockCandleRepository candleRepo,
        IHarvestJobRepository jobRepo,
        ITradierClient tradierClient,
        IThetaDataClient thetaDataClient,
        IMarketDataClient marketDataClient,
        ILogger<HarvestOrchestrator> logger)
    {
        _watchlistRepo = watchlistRepo;
        _optionRepo = optionRepo;
        _candleRepo = candleRepo;
        _jobRepo = jobRepo;
        _tradierClient = tradierClient;
        _thetaDataClient = thetaDataClient;
        _marketDataClient = marketDataClient;
        _logger = logger;
    }

    public async Task<DataHarvestJob> RunDailyHarvestAsync(CancellationToken ct = default)
    {
        var job = new DataHarvestJob
        {
            Id = Guid.NewGuid(),
            JobType = JobType.DailyTradierHarvest,
            TargetDateRange = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            Status = JobStatus.Running,
            StartedAt = DateTime.UtcNow,
            ExecutionLog = "Starting automated fault-tolerant EOD harvest & catch-up..."
        };

        await _jobRepo.CreateJobAsync(job, ct);

        try
        {
            var symbols = await _watchlistRepo.GetAllAsync(ct);
            var activeSymbols = symbols.Where(s => s.IsActiveHarvesting).ToList();
            int totalInserted = 0;
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var logLines = new List<string> { $"Found {activeSymbols.Count} active symbols to harvest. Evaluating calendar completeness..." };

            foreach (var sym in activeSymbols)
            {
                try
                {
                    // Check latest available date to detect any missed days during offline/weekend periods
                    var availableDates = await _optionRepo.GetAvailableDatesAsync(sym.Symbol, ct);
                    DateOnly startDateForSymbol = today;

                    if (availableDates.Count > 0)
                    {
                        var lastDate = availableDates.Last();
                        // Find all expected trading days between lastDate + 1 and today
                        var nextTradingDay = lastDate.AddDays(1);
                        if (nextTradingDay <= today)
                        {
                            var missedTradingDays = DataIntegrityService.GenerateExpectedTradingDays(nextTradingDay, today);
                            if (missedTradingDays.Count > 0)
                            {
                                logLines.Add($"[{sym.Symbol}] Detected {missedTradingDays.Count} unharvested trading sessions since {lastDate:yyyy-MM-dd}. Backfilling automatically...");
                                foreach (var missedDate in missedTradingDays)
                                {
                                    var (candle, snapshots) = await _tradierClient.FetchDailyEodAsync(sym.Symbol, missedDate, ct);
                                    if (candle != null) await _candleRepo.UpsertCandlesAsync(new[] { candle }, ct);
                                    if (snapshots.Count > 0)
                                    {
                                        var count = await _optionRepo.UpsertSnapshotsAsync(snapshots, ct);
                                        totalInserted += count;
                                        logLines.Add($"[{sym.Symbol}] Backfilled {missedDate:yyyy-MM-dd}: {count} option contracts.");
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fresh symbol without historical snapshots - harvest today
                        if (DataIntegrityService.IsUsMarketTradingDay(today))
                        {
                            var (candle, snapshots) = await _tradierClient.FetchDailyEodAsync(sym.Symbol, today, ct);
                            if (candle != null) await _candleRepo.UpsertCandlesAsync(new[] { candle }, ct);
                            if (snapshots.Count > 0)
                            {
                                var count = await _optionRepo.UpsertSnapshotsAsync(snapshots, ct);
                                totalInserted += count;
                                logLines.Add($"[{sym.Symbol}] Initial harvest for {today:yyyy-MM-dd}: {count} contracts.");
                            }
                        }
                    }

                    await _watchlistRepo.UpdateCoverageStatsAsync(sym.Symbol, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to harvest {Symbol}", sym.Symbol);
                    logLines.Add($"[{sym.Symbol}] ERROR: {ex.Message}");
                }
            }

            job.Status = JobStatus.Completed;
            job.RowsInserted = totalInserted;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog = string.Join("\n", logLines);
            await _jobRepo.UpdateJobAsync(job, ct);

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Daily harvest failed.");
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog += $"\nCRITICAL FAILURE: {ex.Message}";
            await _jobRepo.UpdateJobAsync(job, ct);
            return job;
        }
    }

    public async Task<int> RunCatchupBackfillAsync(CancellationToken ct = default)
    {
        var job = await RunDailyHarvestAsync(ct);
        return job.RowsInserted;
    }

    public async Task<DataHarvestJob> TriggerSeedAsync(string symbol, JobType source, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var job = new DataHarvestJob
        {
            Id = Guid.NewGuid(),
            JobType = source,
            Symbol = symbol,
            TargetDateRange = $"{from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            Status = JobStatus.Running,
            StartedAt = DateTime.UtcNow,
            ExecutionLog = $"Triggering historical seed for {symbol} ({from:yyyy-MM-dd} to {to:yyyy-MM-dd}) via {source}..."
        };

        await _jobRepo.CreateJobAsync(job, ct);

        try
        {
            int rowsCount = 0;
            int credits = 0;

            await _watchlistRepo.AddOrUpdateAsync(new WatchlistSymbol
            {
                Symbol = symbol,
                AssetType = AssetType.Equity,
                IsActiveHarvesting = true
            }, ct);

            List<HistoricalStockCandle> candles = new();

            if (source == JobType.ThetaDataSeed)
            {
                try
                {
                    candles = await _thetaDataClient.FetchHistoricalStockCandlesAsync(symbol, from, to, ct);
                    if (candles.Count > 0)
                    {
                        await _candleRepo.UpsertCandlesAsync(candles, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch stock candles from ThetaData for {Symbol}", symbol);
                }

                var snapshots = await _thetaDataClient.FetchEodHistoricalRangeAsync(symbol, from, to, ct);
                rowsCount = await _optionRepo.UpsertSnapshotsAsync(snapshots, ct);
            }
            else if (source == JobType.MarketDataSeed)
            {
                var current = from;
                var allSnaps = new List<HistoricalOptionSnapshot>();
                while (current <= to)
                {
                    if (DataIntegrityService.IsUsMarketTradingDay(current))
                    {
                        var snaps = await _marketDataClient.FetchFilteredOptionsAsync(symbol, current, 7, 45, OptionSide.Call, ct);
                        allSnaps.AddRange(snaps);
                        credits += 1;
                    }
                    current = current.AddDays(1);
                }
                rowsCount = await _optionRepo.UpsertSnapshotsAsync(allSnaps, ct);
            }
            else
            {
                try
                {
                    candles = await _tradierClient.FetchHistoricalStockCandlesAsync(symbol, from, to, ct);
                    if (candles.Count > 0)
                    {
                        await _candleRepo.UpsertCandlesAsync(candles, ct);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch stock candles from Tradier for {Symbol}", symbol);
                }

                var current = from;
                var allSnaps = new List<HistoricalOptionSnapshot>();
                while (current <= to)
                {
                    if (DataIntegrityService.IsUsMarketTradingDay(current))
                    {
                        var (_, snaps) = await _tradierClient.FetchDailyEodAsync(symbol, current, ct);
                        allSnaps.AddRange(snaps);
                    }
                    current = current.AddDays(1);
                }
                rowsCount = await _optionRepo.UpsertSnapshotsAsync(allSnaps, ct);
            }

            await _watchlistRepo.UpdateCoverageStatsAsync(symbol, ct);

            job.Status = JobStatus.Completed;
            job.RowsInserted = rowsCount;
            job.CreditsConsumed = credits;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog += $"\nSuccessfully ingested {rowsCount} option snapshot rows and {candles.Count} stock candles.";
            await _jobRepo.UpdateJobAsync(job, ct);

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Historical seed failed for {Symbol}", symbol);
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog += $"\nERROR: {ex.Message}";
            await _jobRepo.UpdateJobAsync(job, ct);
            return job;
        }
    }
}
