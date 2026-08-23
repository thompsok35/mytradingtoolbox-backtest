using MyTradingToolbox.Core.Enums;

namespace MyTradingToolbox.Core.Models;

public enum PositionSizingMode
{
    FixedContracts = 1,
    FixedDollarBudget = 2,
    PortfolioCompoundingPercent = 3
}

public class BacktestRequest
{
    public string Strategy { get; set; } = "ITM_COVERED_CALL";
    public string Symbol { get; set; } = "AAPL";
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal InitialCapital { get; set; } = 50000m;
    
    // Position Sizing Methodology
    public PositionSizingMode SizingMode { get; set; } = PositionSizingMode.FixedContracts;
    public int FixedContracts { get; set; } = 1;
    public decimal FixedDollarBudget { get; set; } = 2500m;
    public decimal AllocationPercent { get; set; } = 0.10m; // 10% of portfolio cash

    // ITM Covered Call Risk Rules & Strategy Criteria
    public decimal MinAnnualizedRocPercent { get; set; } = 20.0m; // Min 20% annualized return on assignment
    public decimal MinDownsideBufferPercent { get; set; } = 5.0m; // Min 5% downside cushion (CallPremium / SpotPrice)
    public decimal TargetDelta { get; set; } = 0.85m;
    public decimal DeltaTolerance { get; set; } = 0.15m;
    public int TargetDte { get; set; } = 7;
    public int MinDte { get; set; } = 7;
    public int MaxDte { get; set; } = 13;
    public decimal? ProfitTargetPercent { get; set; } // Optional early take-profit (held to expiration by default)
    public decimal? StopLossPercent { get; set; } // e.g. 2.0 (200% loss)
    public bool RollOnDeltaBreach { get; set; } = true;
    public decimal RollDeltaThreshold { get; set; } = 0.50m; // Roll if delta drops below 0.50 (loses ITM safety)
    public int CloseDteThreshold { get; set; } = 2; // Close/roll 2 days prior to expiration to avoid pin risk
    public decimal SlippagePerContract { get; set; } = 0.02m;
    public decimal CommissionPerContract { get; set; } = 0.65m;
}

public class BacktestResult
{
    public string StrategyName { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public decimal InitialCapital { get; set; }
    public BacktestRequest Parameters { get; set; } = new();
    public PerformanceMetrics Metrics { get; set; } = new();
    public List<BacktestTrade> Trades { get; set; } = [];
    public List<EquityPoint> DailyEquityCurve { get; set; } = [];
}

public class BacktestTrade
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public int TradeNumber { get; set; }
    public DateOnly EntryDate { get; set; }
    public DateOnly ExitDate { get; set; }
    public int Contracts { get; set; } = 1;
    public decimal StockEntryPrice { get; set; }
    public decimal StockExitPrice { get; set; }
    public string OptionSymbol { get; set; } = string.Empty;
    public decimal Strike { get; set; }
    public DateOnly ExpirationDate { get; set; }
    public decimal EntryDelta { get; set; }
    public decimal EntryProbITM { get; set; }
    public decimal OptionEntryPremium { get; set; }
    public decimal OptionExitPremium { get; set; }
    public decimal NetDebitPaid { get; set; }
    public decimal TotalDebitOutlay { get; set; }
    public decimal NetCreditReceived { get; set; }
    public decimal RealizedPnlDollars { get; set; }
    public decimal ReturnOnCapitalPercent { get; set; }
    public int HoldDays { get; set; }
    public BacktestTradeType TradeType { get; set; } = BacktestTradeType.BuyWrite;
    public decimal AdjustedCostBasisPerShare { get; set; }
    public ExitReason ExitReason { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public class EquityPoint
{
    public DateOnly Date { get; set; }
    public decimal Cash { get; set; }
    public decimal StockValue { get; set; }
    public decimal OptionValue { get; set; }
    public decimal TotalEquity { get; set; }
    public decimal DrawdownPercent { get; set; }
    public decimal BenchmarkEquity { get; set; }
    public decimal BenchmarkReturnPercent { get; set; }
}

public class PerformanceMetrics
{
    public decimal InitialCapital { get; set; }
    public decimal FinalEquity { get; set; }
    public decimal TotalNetProfit { get; set; }
    public decimal TotalReturnPercent { get; set; }
    public decimal CAGRPercent { get; set; }
    public decimal BenchmarkReturnPercent { get; set; }
    public decimal BenchmarkCAGRPercent { get; set; }
    public decimal AlphaPercent { get; set; }
    public decimal SharpeRatio { get; set; }
    public decimal SortinoRatio { get; set; }
    public decimal MaxDrawdownPercent { get; set; }
    public decimal WinRatePercent { get; set; }
    public int TotalTrades { get; set; }
    public int WinningTrades { get; set; }
    public int LosingTrades { get; set; }
    public decimal ProfitFactor { get; set; }
    public decimal AverageTradePnl { get; set; }
    public decimal AverageWinningTradePnl { get; set; }
    public decimal AverageLosingTradePnl { get; set; }
    public decimal AverageHoldDays { get; set; }
    public decimal AnnualizedVolatility { get; set; }
}
