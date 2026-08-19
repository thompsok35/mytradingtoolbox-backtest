using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class StockCandleRepository : IStockCandleRepository
{
    private readonly MarketDataContext _db;

    public StockCandleRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<List<HistoricalStockCandle>> GetCandlesAsync(string symbol, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.HistoricalStockCandles
            .AsNoTracking()
            .Where(c => c.Symbol == sym && c.Date >= from && c.Date <= to)
            .OrderBy(c => c.Date)
            .ToListAsync(ct);
    }

    public async Task<HistoricalStockCandle?> GetLatestCandleAsync(string symbol, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.HistoricalStockCandles
            .AsNoTracking()
            .Where(c => c.Symbol == sym)
            .OrderByDescending(c => c.Date)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> UpsertCandlesAsync(IEnumerable<HistoricalStockCandle> candles, CancellationToken ct = default)
    {
        var list = candles.ToList();
        if (list.Count == 0) return 0;

        int count = 0;
        var symbols = list.Select(c => c.Symbol.Trim().ToUpperInvariant()).Distinct().ToList();
        var minDate = list.Min(c => c.Date);
        var maxDate = list.Max(c => c.Date);

        var existing = await _db.HistoricalStockCandles
            .Where(c => symbols.Contains(c.Symbol) && c.Date >= minDate && c.Date <= maxDate)
            .ToDictionaryAsync(c => $"{c.Symbol}_{c.Date:yyyyMMdd}", ct);

        foreach (var candle in list)
        {
            candle.Symbol = candle.Symbol.Trim().ToUpperInvariant();
            var key = $"{candle.Symbol}_{candle.Date:yyyyMMdd}";

            if (existing.TryGetValue(key, out var ex))
            {
                ex.Open = candle.Open;
                ex.High = candle.High;
                ex.Low = candle.Low;
                ex.Close = candle.Close;
                ex.Volume = candle.Volume;
                ex.Vwap = candle.Vwap;
                ex.DataSource = candle.DataSource;
            }
            else
            {
                if (candle.Id == Guid.Empty) candle.Id = Guid.NewGuid();
                candle.CreatedAt = DateTime.UtcNow;
                _db.HistoricalStockCandles.Add(candle);
            }
            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }
}
