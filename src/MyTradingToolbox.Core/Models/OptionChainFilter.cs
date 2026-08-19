using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Models;

public class OptionChainFilter
{
    public string Symbol { get; set; } = string.Empty;
    public DateOnly? Date { get; set; }
    public int? MinDte { get; set; }
    public int? MaxDte { get; set; }
    public decimal? MinStrike { get; set; }
    public decimal? MaxStrike { get; set; }
    public OptionSide? Side { get; set; }
    public decimal? MinDelta { get; set; }
    public decimal? MaxDelta { get; set; }
    public decimal? MinIV { get; set; }
    public decimal? MaxIV { get; set; }
    public DateOnly? ExpirationDate { get; set; }
}
