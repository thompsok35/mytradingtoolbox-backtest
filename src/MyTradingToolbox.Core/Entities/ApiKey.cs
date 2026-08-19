namespace MyTradingToolbox.Core.Entities;

public class ApiKey
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = string.Empty;
    public string ConsumerName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 120;
    public long TotalRequests { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
}
