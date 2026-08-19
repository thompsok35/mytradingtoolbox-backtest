using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class IntegrityRepository : IIntegrityRepository
{
    private readonly MarketDataContext _db;

    public IntegrityRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<DataIntegrityAudit> SaveAuditAsync(DataIntegrityAudit audit, CancellationToken ct = default)
    {
        if (audit.Id == Guid.Empty) audit.Id = Guid.NewGuid();
        audit.CreatedAt = DateTime.UtcNow;
        _db.DataIntegrityAudits.Add(audit);
        await _db.SaveChangesAsync(ct);
        return audit;
    }

    public async Task<DataIntegrityAudit?> GetLatestAuditAsync(string symbol, CancellationToken ct = default)
    {
        var sym = symbol.Trim().ToUpperInvariant();
        return await _db.DataIntegrityAudits
            .AsNoTracking()
            .Where(a => a.Symbol == sym)
            .OrderByDescending(a => a.AuditDate)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<DataIntegrityAudit>> GetAllLatestAuditsAsync(CancellationToken ct = default)
    {
        var symbols = await _db.DataIntegrityAudits.Select(a => a.Symbol).Distinct().ToListAsync(ct);
        var result = new List<DataIntegrityAudit>();

        foreach (var sym in symbols)
        {
            var latest = await _db.DataIntegrityAudits
                .AsNoTracking()
                .Where(a => a.Symbol == sym)
                .OrderByDescending(a => a.AuditDate)
                .FirstOrDefaultAsync(ct);
            if (latest != null) result.Add(latest);
        }

        return result;
    }
}
