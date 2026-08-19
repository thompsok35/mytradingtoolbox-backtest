using MyTradingToolbox.Core.Entities;

namespace MyTradingToolbox.Core.Interfaces;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AppUser?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<AppUser> CreateOrUpdateGoogleUserAsync(string email, string name, string? pictureUrl, CancellationToken ct = default);
    Task UpdateUserAsync(AppUser user, CancellationToken ct = default);
    Task<List<AppUser>> GetAllUsersAsync(CancellationToken ct = default);
}
