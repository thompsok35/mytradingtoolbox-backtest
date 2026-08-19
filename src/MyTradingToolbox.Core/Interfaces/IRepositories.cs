using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Models;

namespace MyTradingToolbox.Core.Interfaces;

public interface IWatchlistRepository
{
    Task<List<WatchlistSymbol>> GetAllAsync(CancellationToken ct = default);
    Task<WatchlistSymbol?> GetBySymbolAsync(string symbol, CancellationToken ct = default);
    Task<WatchlistSymbol> AddOrUpdateAsync(WatchlistSymbol symbol, CancellationToken ct = default);
    Task<bool> ToggleHarvestingAsync(string symbol, bool isActive, CancellationToken ct = default);
    Task UpdateCoverageStatsAsync(string symbol, CancellationToken ct = default);
    Task<bool> DeleteAsync(string symbol, CancellationToken ct = default);
}

public interface IOptionSnapshotRepository
{
    Task<List<HistoricalOptionSnapshot>> GetChainAsync(OptionChainFilter filter, CancellationToken ct = default);
    Task<List<HistoricalOptionSnapshot>> GetQuotesByOptionSymbolAsync(string optionSymbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);
    Task<int> UpsertSnapshotsAsync(IEnumerable<HistoricalOptionSnapshot> snapshots, CancellationToken ct = default);
    Task<List<DateOnly>> GetAvailableDatesAsync(string symbol, CancellationToken ct = default);
    Task<int> GetTotalRowsCountAsync(string? symbol = null, CancellationToken ct = default);
}

public interface IStockCandleRepository
{
    Task<List<HistoricalStockCandle>> GetCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<HistoricalStockCandle?> GetLatestCandleAsync(string symbol, CancellationToken ct = default);
    Task<int> UpsertCandlesAsync(IEnumerable<HistoricalStockCandle> candles, CancellationToken ct = default);
}

public interface IHarvestJobRepository
{
    Task<DataHarvestJob> CreateJobAsync(DataHarvestJob job, CancellationToken ct = default);
    Task UpdateJobAsync(DataHarvestJob job, CancellationToken ct = default);
    Task<List<DataHarvestJob>> GetRecentJobsAsync(int count = 50, CancellationToken ct = default);
    Task<DataHarvestJob?> GetJobByIdAsync(Guid id, CancellationToken ct = default);
}

public interface IIntegrityRepository
{
    Task<DataIntegrityAudit> SaveAuditAsync(DataIntegrityAudit audit, CancellationToken ct = default);
    Task<DataIntegrityAudit?> GetLatestAuditAsync(string symbol, CancellationToken ct = default);
    Task<List<DataIntegrityAudit>> GetAllLatestAuditsAsync(CancellationToken ct = default);
}

public interface IApiKeyRepository
{
    Task<ApiKey?> ValidateKeyAsync(string key, CancellationToken ct = default);
    Task<List<ApiKey>> GetAllKeysAsync(CancellationToken ct = default);
    Task<ApiKey> CreateKeyAsync(string consumerName, int rateLimitPerMin = 120, DateTime? expiresAt = null, CancellationToken ct = default);
    Task<bool> RevokeKeyAsync(Guid id, CancellationToken ct = default);
    Task LogUsageAsync(ApiUsageLog log, CancellationToken ct = default);
    Task<List<ApiUsageLog>> GetRecentLogsAsync(int count = 100, CancellationToken ct = default);
}
