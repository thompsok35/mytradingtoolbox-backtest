using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Entities;

public class HistoricalStockCandle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Symbol { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? Vwap { get; set; }
    public DataSource DataSource { get; set; } = DataSource.Tradier;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
