using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class ApiKeyRepository : IApiKeyRepository
{
    private readonly MarketDataContext _db;

    public ApiKeyRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<ApiKey?> ValidateKeyAsync(string key, CancellationToken ct = default)
    {
        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Key == key && k.IsActive, ct);
        if (apiKey == null) return null;

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            return null;
        }

        apiKey.TotalRequests++;
        apiKey.LastUsedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return apiKey;
    }

    public async Task<List<ApiKey>> GetAllKeysAsync(CancellationToken ct = default)
    {
        return await _db.ApiKeys.AsNoTracking().OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
    }

    public async Task<ApiKey> CreateKeyAsync(string consumerName, int rateLimitPerMin = 120, DateTime? expiresAt = null, CancellationToken ct = default)
    {
        var key = new ApiKey
        {
            Id = Guid.NewGuid(),
            Key = $"mtt_{GenerateSecureToken(32)}",
            ConsumerName = consumerName.Trim(),
            IsActive = true,
            RateLimitPerMinute = rateLimitPerMin,
            TotalRequests = 0,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(ct);
        return key;
    }

    public async Task<bool> RevokeKeyAsync(Guid id, CancellationToken ct = default)
    {
        var item = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (item == null) return false;

        item.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task LogUsageAsync(ApiUsageLog log, CancellationToken ct = default)
    {
        if (log.Id == Guid.Empty) log.Id = Guid.NewGuid();
        log.Timestamp = DateTime.UtcNow;
        _db.ApiUsageLogs.Add(log);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<ApiUsageLog>> GetRecentLogsAsync(int count = 100, CancellationToken ct = default)
    {
        return await _db.ApiUsageLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Take(count)
            .ToListAsync(ct);
    }

    private static string GenerateSecureToken(int length)
    {
        var bytes = RandomNumberGenerator.GetBytes(length);
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
