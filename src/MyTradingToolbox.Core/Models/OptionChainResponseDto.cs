using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Models;

public class OptionChainResponseDto
{
    public string Symbol { get; set; } = string.Empty;
    public DateOnly SnapshotDate { get; set; }
    public decimal UnderlyingPrice { get; set; }
    public List<OptionContractDto> Calls { get; set; } = [];
    public List<OptionContractDto> Puts { get; set; } = [];
}

public class OptionContractDto
{
    public string OptionSymbol { get; set; } = string.Empty;
    public DateOnly ExpirationDate { get; set; }
    public int DTE { get; set; }
    public decimal Strike { get; set; }
    public OptionSide Side { get; set; }
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
    public long Volume { get; set; }
    public long OpenInterest { get; set; }
    public DataSource DataSource { get; set; }
}
