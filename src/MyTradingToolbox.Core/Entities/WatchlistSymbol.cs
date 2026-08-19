using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Entities;

public class WatchlistSymbol
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public AssetType AssetType { get; set; } = AssetType.Equity;
    public bool IsActiveHarvesting { get; set; } = true;
    public DateOnly? EarliestAvailableDate { get; set; }
    public DateOnly? LatestAvailableDate { get; set; }
    public int TotalSnapshotDays { get; set; }
    public int TotalOptionRows { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
