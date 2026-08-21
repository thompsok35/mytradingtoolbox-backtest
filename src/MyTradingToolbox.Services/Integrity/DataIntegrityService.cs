using System.Text.Json;
using Microsoft.Extensions.Logging;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Enums;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;

namespace MyTradingToolbox.Services.Integrity;

public interface IDataIntegrityService
{
    Task<DataIntegrityAudit> AuditSymbolAsync(string symbol, CancellationToken ct = default);
    Task<MarketCoverageDto> GetCoverageAsync(string symbol, CancellationToken ct = default);
    Task<DataHarvestJob> AutoRepairGapsAsync(string symbol, CancellationToken ct = default);
}

public class DataIntegrityService : IDataIntegrityService
{
    private readonly IOptionSnapshotRepository _optionRepo;
    private readonly IStockCandleRepository _candleRepo;
    private readonly IWatchlistRepository _watchlistRepo;
    private readonly IIntegrityRepository _integrityRepo;
    private readonly IHarvestJobRepository _jobRepo;
    private readonly Harvester.IHarvestOrchestrator _harvester;
    private readonly ILogger<DataIntegrityService> _logger;

    public DataIntegrityService(
        IOptionSnapshotRepository optionRepo,
        IStockCandleRepository candleRepo,
        IWatchlistRepository watchlistRepo,
        IIntegrityRepository integrityRepo,
        IHarvestJobRepository jobRepo,
        Harvester.IHarvestOrchestrator harvester,
        ILogger<DataIntegrityService> logger)
    {
        _optionRepo = optionRepo;
        _candleRepo = candleRepo;
        _watchlistRepo = watchlistRepo;
        _integrityRepo = integrityRepo;
        _jobRepo = jobRepo;
        _harvester = harvester;
        _logger = logger;
    }

    public async Task<DataIntegrityAudit> AuditSymbolAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var dates = await _optionRepo.GetAvailableDatesAsync(symbol, ct);
        var audit = new DataIntegrityAudit
        {
            Id = Guid.NewGuid(),
            Symbol = symbol,
            AuditDate = DateOnly.FromDateTime(DateTime.UtcNow),
            CreatedAt = DateTime.UtcNow
        };

        if (dates.Count == 0)
        {
            audit.TotalExpectedTradingDays = 0;
            audit.ActualDaysPresent = 0;
            audit.MissingDatesJson = "[]";
            audit.CorruptQuotesCount = 0;
            audit.HealthScorePercent = 0m;
            await _integrityRepo.SaveAuditAsync(audit, ct);
            return audit;
        }

        var minDate = dates.First();
        var maxDate = dates.Last();

        var expectedTradingDays = GenerateExpectedTradingDays(minDate, maxDate);
        var dateSet = new HashSet<DateOnly>(dates);

        var missingDays = expectedTradingDays.Where(d => !dateSet.Contains(d)).ToList();
        audit.TotalExpectedTradingDays = expectedTradingDays.Count;
        audit.ActualDaysPresent = dates.Count;
        audit.MissingDatesJson = JsonSerializer.Serialize(missingDays);

        // Check for corrupted quotes (Bid > Ask or Bid < 0 or Strike <= 0)
        var allQuotes = await _optionRepo.GetChainAsync(new OptionChainFilter { Symbol = symbol }, ct);
        int corruptCount = allQuotes.Count(q => q.Bid > q.Ask || q.Bid < 0 || q.Strike <= 0);
        audit.CorruptQuotesCount = corruptCount;

        decimal completenessRatio = expectedTradingDays.Count > 0
            ? (decimal)dates.Count / expectedTradingDays.Count
            : 1.0m;

        decimal health = Math.Clamp(completenessRatio * 100m - (corruptCount > 0 ? 5m : 0m), 0m, 100m);
        audit.HealthScorePercent = Math.Round(health, 2);

        await _integrityRepo.SaveAuditAsync(audit, ct);
        return audit;
    }

    public async Task<MarketCoverageDto> GetCoverageAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var watchlist = await _watchlistRepo.GetBySymbolAsync(symbol, ct);
        var audit = await _integrityRepo.GetLatestAuditAsync(symbol, ct);

        if (audit == null)
        {
            audit = await AuditSymbolAsync(symbol, ct);
        }

        var missingDates = new List<DateOnly>();
        if (!string.IsNullOrWhiteSpace(audit.MissingDatesJson))
        {
            try
            {
                missingDates = JsonSerializer.Deserialize<List<DateOnly>>(audit.MissingDatesJson) ?? [];
            }
            catch { }
        }

        return new MarketCoverageDto
        {
            Symbol = symbol,
            AssetType = watchlist?.AssetType ?? AssetType.Equity,
            IsActiveHarvesting = watchlist?.IsActiveHarvesting ?? true,
            EarliestAvailableDate = watchlist?.EarliestAvailableDate,
            LatestAvailableDate = watchlist?.LatestAvailableDate,
            TotalSnapshotDays = watchlist?.TotalSnapshotDays ?? 0,
            TotalOptionRows = watchlist?.TotalOptionRows ?? 0,
            HealthScorePercent = audit.HealthScorePercent,
            MissingDates = missingDates,
            CorruptQuotesCount = audit.CorruptQuotesCount
        };
    }

    public async Task<DataHarvestJob> AutoRepairGapsAsync(string symbol, CancellationToken ct = default)
    {
        symbol = symbol.Trim().ToUpperInvariant();
        var audit = await AuditSymbolAsync(symbol, ct);
        var missingDates = JsonSerializer.Deserialize<List<DateOnly>>(audit.MissingDatesJson) ?? [];

        var job = new DataHarvestJob
        {
            Id = Guid.NewGuid(),
            JobType = JobType.AutoRepair,
            Symbol = symbol,
            TargetDateRange = $"{missingDates.Count} missing days",
            Status = JobStatus.Running,
            StartedAt = DateTime.UtcNow,
            ExecutionLog = $"Starting auto-repair for {symbol}. Identified {missingDates.Count} missing trading dates..."
        };

        await _jobRepo.CreateJobAsync(job, ct);

        try
        {
            int repairedDays = 0;
            int totalInserted = 0;

            foreach (var date in missingDates)
            {
                var seedJob = await _harvester.TriggerSeedAsync(symbol, JobType.DailyTradierHarvest, date, date, ct);
                totalInserted += seedJob.RowsInserted;
                repairedDays++;
            }

            // Re-run audit
            await AuditSymbolAsync(symbol, ct);
            await _watchlistRepo.UpdateCoverageStatsAsync(symbol, ct);

            job.Status = JobStatus.Completed;
            job.RowsInserted = totalInserted;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog += $"\nAuto-repair finished. Backfilled {repairedDays} dates with {totalInserted} option records.";
            await _jobRepo.UpdateJobAsync(job, ct);

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto repair failed for {Symbol}", symbol);
            job.Status = JobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            job.ExecutionLog += $"\nERROR: {ex.Message}";
            await _jobRepo.UpdateJobAsync(job, ct);
            return job;
        }
    }

    public static List<DateOnly> GenerateExpectedTradingDays(DateOnly from, DateOnly to)
    {
        var result = new List<DateOnly>();
        var current = from;

        while (current <= to)
        {
            if (IsUsMarketTradingDay(current))
            {
                result.Add(current);
            }
            current = current.AddDays(1);
        }

        return result;
    }

    public static bool IsUsMarketTradingDay(DateOnly date)
    {
        if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            return false;

        // New Year's Day (Jan 1)
        if (date.Month == 1 && date.Day == 1) return false;
        // MLK Day: 3rd Monday in January
        if (date.Month == 1 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 15 && date.Day <= 21) return false;
        // Washington's Birthday: 3rd Monday in February
        if (date.Month == 2 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 15 && date.Day <= 21) return false;
        // Memorial Day: Last Monday in May
        if (date.Month == 5 && date.DayOfWeek == DayOfWeek.Monday && date.Day >= 25) return false;
        // Juneteenth (June 19)
        if (date.Month == 6 && date.Day == 19) return false;
        // Independence Day (July 4)
        if (date.Month == 7 && date.Day == 4) return false;
        // Labor Day: 1st Monday in September
        if (date.Month == 9 && date.DayOfWeek == DayOfWeek.Monday && date.Day <= 7) return false;
        // Thanksgiving: 4th Thursday in November
        if (date.Month == 11 && date.DayOfWeek == DayOfWeek.Thursday && date.Day >= 22 && date.Day <= 28) return false;
        // Christmas (Dec 25)
        if (date.Month == 12 && date.Day == 25) return false;

        return true;
    }
}
