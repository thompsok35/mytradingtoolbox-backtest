using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Core.Models;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class OptionSnapshotRepository : IOptionSnapshotRepository
{
    private readonly MarketDataContext _db;

    public OptionSnapshotRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<List<HistoricalOptionSnapshot>> GetChainAsync(OptionChainFilter filter, CancellationToken ct = default)
    {
        var query = _db.HistoricalOptionSnapshots.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Symbol))
        {
            var sym = filter.Symbol.Trim().ToUpperInvariant();
            query = query.Where(o => o.UnderlyingSymbol == sym);
        }

        if (filter.Date.HasValue)
        {
            query = query.Where(o => o.SnapshotDate == filter.Date.Value);
        }

        if (filter.MinDte.HasValue)
            query = query.Where(o => o.DTE >= filter.MinDte.Value);

        if (filter.MaxDte.HasValue)
            query = query.Where(o => o.DTE <= filter.MaxDte.Value);

        if (filter.MinStrike.HasValue)
            query = query.Where(o => o.Strike >= filter.MinStrike.Value);

        if (filter.MaxStrike.HasValue)
            query = query.Where(o => o.Strike <= filter.MaxStrike.Value);

        if (filter.Side.HasValue)
            query = query.Where(o => o.Side == filter.Side.Value);

        if (filter.MinDelta.HasValue)
            query = query.Where(o => o.Delta >= filter.MinDelta.Value);

        if (filter.MaxDelta.HasValue)
            query = query.Where(o => o.Delta <= filter.MaxDelta.Value);

        if (filter.MinIV.HasValue)
            query = query.Where(o => o.ImpliedVolatility >= filter.MinIV.Value);

        if (filter.MaxIV.HasValue)
            query = query.Where(o => o.ImpliedVolatility <= filter.MaxIV.Value);

        if (filter.ExpirationDate.HasValue)
            query = query.Where(o => o.ExpirationDate == filter.ExpirationDate.Value);

        return await query.OrderBy(o => o.ExpirationDate).ThenBy(o => o.Strike).ThenBy(o => o.Side).ToListAsync(ct);
    }

    public async Task<List<HistoricalOptionSnapshot>> GetQuotesByOptionSymbolAsync(string optionSymbol, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default)
    {
        var opt = optionSymbol.Trim().ToUpperInvariant();
        var query = _db.HistoricalOptionSnapshots.AsNoTracking().Where(o => o.OptionSymbol == opt);

        if (from.HasValue)
            query = query.Where(o => o.SnapshotDate >= from.Value);

        if (to.HasValue)
            query = query.Where(o => o.SnapshotDate <= to.Value);

        return await query.OrderBy(o => o.SnapshotDate).ToListAsync(ct);
    }

    public async Task<int> UpsertSnapshotsAsync(IEnumerable<HistoricalOptionSnapshot> snapshots, CancellationToken ct = default)
    {
        var list = snapshots.ToList();
        if (list.Count == 0) return 0;

        // Group by SnapshotDate & UnderlyingSymbol to batch process efficiently
        int insertedOrUpdated = 0;
        var batchGroups = list.GroupBy(s => new { Sym = s.UnderlyingSymbol.Trim().ToUpperInvariant(), s.SnapshotDate });

        foreach (var group in batchGroups)
        {
            var sym = group.Key.Sym;
            var date = group.Key.SnapshotDate;

            // Fetch existing snapshots for this symbol and date
            var existingKeys = await _db.HistoricalOptionSnapshots
                .Where(o => o.UnderlyingSymbol == sym && o.SnapshotDate == date)
                .ToDictionaryAsync(o => o.OptionSymbol, ct);

            // Deduplicate incoming items within the same batch group
            var uniqueItems = group
                .GroupBy(i => i.OptionSymbol.Trim().ToUpperInvariant())
                .Select(g => g.Last());

            foreach (var item in uniqueItems)
            {
                item.UnderlyingSymbol = sym;
                item.OptionSymbol = item.OptionSymbol.Trim().ToUpperInvariant();
                item.Mid = (item.Bid + item.Ask) / 2m;

                if (existingKeys.TryGetValue(item.OptionSymbol, out var existing))
                {
                    existing.Bid = item.Bid;
                    existing.Ask = item.Ask;
                    existing.Mid = item.Mid;
                    existing.Last = item.Last;
                    existing.Delta = item.Delta;
                    existing.Gamma = item.Gamma;
                    existing.Theta = item.Theta;
                    existing.Vega = item.Vega;
                    existing.Rho = item.Rho;
                    existing.ImpliedVolatility = item.ImpliedVolatility;
                    existing.UnderlyingPrice = item.UnderlyingPrice;
                    existing.Volume = item.Volume;
                    existing.OpenInterest = item.OpenInterest;
                    existing.DataSource = item.DataSource;
                    existing.DTE = item.DTE;
                }
                else
                {
                    if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
                    item.CreatedAt = DateTime.UtcNow;
                    _db.HistoricalOptionSnapshots.Add(item);
                    existingKeys[item.OptionSymbol] = item;
                }
                insertedOrUpdated++;
            }

            await _db.SaveChangesAsync(ct);
        }

        return insertedOrUpdated;
    }

    public async Task<List<DateOnly>> GetAvailableDatesAsync(string symbol, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.HistoricalOptionSnapshots
            .AsNoTracking()
            .Where(o => o.UnderlyingSymbol == sym)
            .Select(o => o.SnapshotDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);
    }

    public async Task<int> GetTotalRowsCountAsync(string? symbol = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            return await _db.HistoricalOptionSnapshots.CountAsync(ct);

        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.HistoricalOptionSnapshots.CountAsync(o => o.UnderlyingSymbol == sym, ct);
    }
}
