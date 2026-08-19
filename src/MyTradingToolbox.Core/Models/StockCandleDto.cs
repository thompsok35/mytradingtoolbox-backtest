using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Models;

public class StockCandleDto
{
    public string Symbol { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
    public long Volume { get; set; }
    public decimal? Vwap { get; set; }
    public DataSource DataSource { get; set; }
}
