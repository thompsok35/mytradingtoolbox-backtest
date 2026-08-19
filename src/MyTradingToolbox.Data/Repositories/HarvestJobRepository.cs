using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class HarvestJobRepository : IHarvestJobRepository
{
    private readonly MarketDataContext _db;

    public HarvestJobRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<DataHarvestJob> CreateJobAsync(DataHarvestJob job, CancellationToken ct = default)
    {
        if (job.Id == Guid.Empty) job.Id = Guid.NewGuid();
        job.StartedAt = DateTime.UtcNow;
        _db.DataHarvestJobs.Add(job);
        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task UpdateJobAsync(DataHarvestJob job, CancellationToken ct = default)
    {
        _db.DataHarvestJobs.Update(job);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<DataHarvestJob>> GetRecentJobsAsync(int count = 50, CancellationToken ct = default)
    {
        return await _db.DataHarvestJobs
            .AsNoTracking()
            .OrderByDescending(j => j.StartedAt)
            .Take(count)
            .ToListAsync(ct);
    }

    public async Task<DataHarvestJob?> GetJobByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.DataHarvestJobs.FirstOrDefaultAsync(j => j.Id == id, ct);
    }
}
