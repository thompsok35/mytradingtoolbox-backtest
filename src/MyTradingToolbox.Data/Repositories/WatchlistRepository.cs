using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class WatchlistRepository : IWatchlistRepository
{
    private readonly MarketDataContext _db;

    public WatchlistRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<List<WatchlistSymbol>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.WatchlistSymbols.AsNoTracking().OrderBy(s => s.Symbol).ToListAsync(ct);
    }

    public async Task<WatchlistSymbol?> GetBySymbolAsync(string symbol, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.WatchlistSymbols.FirstOrDefaultAsync(s => s.Symbol == sym, ct);
    }

    public async Task<WatchlistSymbol> AddOrUpdateAsync(WatchlistSymbol symbol, CancellationToken ct = default)
    {
        symbol.Symbol = symbol.Symbol.Trim().ToUpperInvariant();
        var existing = await _db.WatchlistSymbols.FirstOrDefaultAsync(s => s.Symbol == symbol.Symbol, ct);

        if (existing == null)
        {
            symbol.Id = Guid.NewGuid();
            symbol.CreatedAt = DateTime.UtcNow;
            symbol.UpdatedAt = DateTime.UtcNow;
            _db.WatchlistSymbols.Add(symbol);
            await _db.SaveChangesAsync(ct);
            return symbol;
        }

        existing.AssetType = symbol.AssetType;
        existing.IsActiveHarvesting = symbol.IsActiveHarvesting;
        existing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<bool> ToggleHarvestingAsync(string symbol, bool isActive, CancellationToken ct = default)
    {
        var item = await GetBySymbolAsync(symbol, ct);
        if (item == null) return false;

        item.IsActiveHarvesting = isActive;
        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task UpdateCoverageStatsAsync(string symbol, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        var item = await _db.WatchlistSymbols.FirstOrDefaultAsync(s => s.Symbol == sym, ct);
        if (item == null) return;

        var dates = await _db.HistoricalOptionSnapshots
            .Where(o => o.UnderlyingSymbol == sym)
            .Select(o => o.SnapshotDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);

        var totalRows = await _db.HistoricalOptionSnapshots
            .CountAsync(o => o.UnderlyingSymbol == sym, ct);

        if (dates.Count > 0)
        {
            item.EarliestAvailableDate = dates.First();
            item.LatestAvailableDate = dates.Last();
            item.TotalSnapshotDays = dates.Count;
            item.TotalOptionRows = totalRows;
        }
        else
        {
            item.EarliestAvailableDate = null;
            item.LatestAvailableDate = null;
            item.TotalSnapshotDays = 0;
            item.TotalOptionRows = 0;
        }

        item.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(string symbol, CancellationToken ct = default)
    {
        var item = await GetBySymbolAsync(symbol, ct);
        if (item == null) return false;

        _db.WatchlistSymbols.Remove(item);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
