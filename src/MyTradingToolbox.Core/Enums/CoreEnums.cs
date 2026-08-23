namespace MyTradingToolbox.Core.Enums;

public enum AssetType
{
    Equity,
    ETF,
    Index
}

public enum OptionSide
{
    Call,
    Put
}

public enum JobType
{
    DailyTradierHarvest,
    ThetaDataSeed,
    MarketDataSeed,
    CSVImport,
    IntegrityCheck,
    AutoRepair
}

public enum JobStatus
{
    Pending,
    Running,
    Completed,
    Failed
}

public enum DataSource
{
    Tradier,
    TradierEOD,
    MarketData,
    ThetaData,
    CSVImport,
    Synthetic
}

public enum TradeAction
{
    BuyToOpen,
    SellToOpen,
    BuyToClose,
    SellToClose,
    Assigned,
    Expired,
    Rolled
}

public enum ExitReason
{
    ProfitTargetHit,
    StopLossHit,
    Expiration,
    Assignment,
    DeltaBreachRoll,
    DteThresholdExit,
    ManualClose
}

public enum BacktestTradeType
{
    BuyWrite,
    CoveredCallRoll,
    CoveredCallNextCycle
}

