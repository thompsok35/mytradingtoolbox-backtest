using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Models;

public class MarketCoverageDto
{
    public string Symbol { get; set; } = string.Empty;
    public AssetType AssetType { get; set; }
    public bool IsActiveHarvesting { get; set; }
    public DateOnly? EarliestAvailableDate { get; set; }
    public DateOnly? LatestAvailableDate { get; set; }
    public int TotalSnapshotDays { get; set; }
    public int TotalOptionRows { get; set; }
    public decimal HealthScorePercent { get; set; }
    public List<DateOnly> MissingDates { get; set; } = [];
    public int CorruptQuotesCount { get; set; }
}
