using Microsoft.EntityFrameworkCore;
using MyTradingToolbox.Core.Entities;
using MyTradingToolbox.Core.Interfaces;
using MyTradingToolbox.Data.Context;

namespace MyTradingToolbox.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly MarketDataContext _db;

    public UserRepository(MarketDataContext db)
    {
        _db = db;
    }

    public async Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        return await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
    }

    public async Task<AppUser> CreateOrUpdateGoogleUserAsync(string email, string name, string? pictureUrl, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        var existing = await _db.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == email, ct);
        if (existing == null)
        {
            existing = new AppUser
            {
                Id = Guid.NewGuid(),
                Email = email,
                Name = name,
                PictureUrl = pictureUrl,
                Role = "Admin",
                IsTwoFactorEnabled = false,
                CreatedAt = DateTime.UtcNow,
                LastLoginAt = DateTime.UtcNow
            };
            _db.Users.Add(existing);
        }
        else
        {
            existing.Name = name;
            existing.PictureUrl = pictureUrl ?? existing.PictureUrl;
            existing.LastLoginAt = DateTime.UtcNow;
            _db.Users.Update(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task UpdateUserAsync(AppUser user, CancellationToken ct = default)
    {
        _db.Users.Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<List<AppUser>> GetAllUsersAsync(CancellationToken ct = default)
    {
        return await _db.Users.AsNoTracking().OrderByDescending(u => u.CreatedAt).ToListAsync(ct);
    }
}
