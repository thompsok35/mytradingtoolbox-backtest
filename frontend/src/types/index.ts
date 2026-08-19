export type AssetType = 'Equity' | 'ETF' | 'Index';
export type OptionSide = 'Call' | 'Put';
export type JobType = 'DailyTradierHarvest' | 'ThetaDataSeed' | 'MarketDataSeed' | 'CSVImport' | 'IntegrityCheck' | 'AutoRepair';
export type JobStatus = 'Pending' | 'Running' | 'Completed' | 'Failed';
export type DataSource = 'Tradier' | 'TradierEOD' | 'MarketData' | 'ThetaData' | 'CSVImport' | 'Synthetic';
export type ExitReason = 'ProfitTargetHit' | 'StopLossHit' | 'Expiration' | 'Assignment' | 'DeltaBreachRoll' | 'DteThresholdExit' | 'ManualClose';

export interface WatchlistSymbol {
  id: string;
  symbol: string;
  assetType: AssetType;
  isActiveHarvesting: boolean;
  earliestAvailableDate?: string;
  latestAvailableDate?: string;
  totalSnapshotDays: number;
  totalOptionRows: number;
  createdAt: string;
  updatedAt: string;
}

export interface OptionContractDto {
  optionSymbol: string;
  expirationDate: string;
  dte: number;
  strike: number;
  side: OptionSide;
  bid: number;
  ask: number;
  mid: number;
  last: number;
  delta?: number;
  gamma?: number;
  theta?: number;
  vega?: number;
  rho?: number;
  impliedVolatility?: number;
  volume: number;
  openInterest: number;
  dataSource: DataSource;
}

export interface OptionChainResponseDto {
  symbol: string;
  snapshotDate: string;
  underlyingPrice: number;
  calls: OptionContractDto[];
  puts: OptionContractDto[];
}

export interface StockCandleDto {
  id: string;
  symbol: string;
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  vwap?: number;
  dataSource: DataSource;
}

export interface MarketCoverageDto {
  symbol: string;
  assetType: AssetType;
  isActiveHarvesting: boolean;
  earliestAvailableDate?: string;
  latestAvailableDate?: string;
  totalSnapshotDays: number;
  totalOptionRows: number;
  healthScorePercent: number;
  missingDates: string[];
  corruptQuotesCount: number;
}

export interface DataHarvestJob {
  id: string;
  jobType: JobType;
  symbol?: string;
  targetDateRange?: string;
  status: JobStatus;
  rowsInserted: number;
  creditsConsumed: number;
  executionLog?: string;
  startedAt?: string;
  completedAt?: string;
}

export interface DataIntegrityAudit {
  id: string;
  symbol: string;
  auditDate: string;
  totalExpectedTradingDays: number;
  actualDaysPresent: number;
  missingDatesJson: string;
  corruptQuotesCount: number;
  healthScorePercent: number;
  createdAt: string;
}

export interface ApiKey {
  id: string;
  key: string;
  consumerName: string;
  isActive: boolean;
  rateLimitPerMinute: number;
  totalRequests: number;
  createdAt: string;
  expiresAt?: string;
  lastUsedAt?: string;
}

export interface ApiUsageLog {
  id: string;
  apiKeyId: string;
  consumerName: string;
  endpoint: string;
  httpMethod: string;
  statusCode: number;
  responseTimeMs: number;
  timestamp: string;
  ipAddress?: string;
}

export interface BacktestRequest {
  strategy: string;
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  targetDelta: number;
  deltaTolerance: number;
  targetDte: number;
  minDte: number;
  maxDte: number;
  profitTargetPercent: number;
  stopLossPercent?: number;
  rollOnDeltaBreach: boolean;
  rollDeltaThreshold: number;
  closeDteThreshold: number;
  slippagePerContract: number;
  commissionPerContract: number;
}

export interface BacktestTrade {
  id: string;
  tradeNumber: number;
  entryDate: string;
  exitDate: string;
  contracts: number;
  stockEntryPrice: number;
  stockExitPrice: number;
  optionSymbol: string;
  strike: number;
  expirationDate: string;
  entryDelta: number;
  optionEntryPremium: number;
  optionExitPremium: number;
  netDebitPaid: number;
  netCreditReceived: number;
  realizedPnlDollars: number;
  returnOnCapitalPercent: number;
  holdDays: number;
  exitReason: ExitReason;
  notes: string;
}

export interface EquityPoint {
  date: string;
  cash: number;
  stockValue: number;
  optionValue: number;
  totalEquity: number;
  drawdownPercent: number;
  benchmarkEquity: number;
  benchmarkReturnPercent: number;
}

export interface PerformanceMetrics {
  initialCapital: number;
  finalEquity: number;
  totalNetProfit: number;
  totalReturnPercent: number;
  cagrPercent: number;
  benchmarkReturnPercent: number;
  benchmarkCAGRPercent: number;
  alphaPercent: number;
  sharpeRatio: number;
  sortinoRatio: number;
  maxDrawdownPercent: number;
  winRatePercent: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  profitFactor: number;
  averageTradePnl: number;
  averageWinningTradePnl: number;
  averageLosingTradePnl: number;
  averageHoldDays: number;
  annualizedVolatility: number;
}

export interface BacktestResult {
  strategyName: string;
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  parameters: BacktestRequest;
  metrics: PerformanceMetrics;
  trades: BacktestTrade[];
  dailyEquityCurve: EquityPoint[];
}
