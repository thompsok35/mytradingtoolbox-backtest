using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Entities;

public class HistoricalOptionSnapshot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UnderlyingSymbol { get; set; } = string.Empty;
    public DateOnly SnapshotDate { get; set; }
    public string OptionSymbol { get; set; } = string.Empty;
    public DateOnly ExpirationDate { get; set; }
    public int DTE { get; set; }
    public decimal Strike { get; set; }
    public OptionSide Side { get; set; } = OptionSide.Call;
    public decimal Bid { get; set; }
    public decimal Ask { get; set; }
    public decimal Mid { get; set; }
    public decimal Last { get; set; }
    public decimal? Delta { get; set; }
    public decimal? Gamma { get; set; }
    public decimal? Theta { get; set; }
    public decimal? Vega { get; set; }
    public decimal? Rho { get; set; }
    public decimal? ImpliedVolatility { get; set; }
    public decimal UnderlyingPrice { get; set; }
    public long Volume { get; set; }
    public long OpenInterest { get; set; }
    public DataSource DataSource { get; set; } = DataSource.TradierEOD;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
