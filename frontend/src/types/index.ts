export type AssetType = 'Equity' | 'ETF' | 'Index' | 'Crypto';
export type OptionSide = 'Call' | 'Put';
export type JobType = 'DailyTradierHarvest' | 'ThetaDataSeed' | 'MarketDataSeed' | 'CSVImport' | 'CsvBulkImport' | 'IntegrityCheck' | 'IntegrityRepair' | 'AutoRepair';
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
  healthScorePercent?: number;
  createdAt: string;
  updatedAt: string;
}

export interface OptionContractDto {
  id?: string;
  underlyingSymbol?: string;
  snapshotDate?: string;
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
  probabilityOfITM?: number;
  underlyingPrice?: number;
  volume: number;
  openInterest: number;
  dataSource?: DataSource | string;
}

export type HistoricalOptionSnapshot = OptionContractDto;

export interface OptionChainResponseDto {
  symbol: string;
  snapshotDate?: string;
  date?: string;
  underlyingPrice?: number;
  spotPrice?: number;
  calls: OptionContractDto[];
  puts: OptionContractDto[];
  availableExpirations?: string[];
}

export interface StockCandleDto {
  id?: string;
  symbol?: string;
  date: string;
  open: number;
  high: number;
  low: number;
  close: number;
  volume: number;
  vwap?: number;
  dataSource?: DataSource;
}

export interface MarketCoverageDto {
  symbol: string;
  assetType?: AssetType;
  isActiveHarvesting?: boolean;
  earliestAvailableDate?: string;
  latestAvailableDate?: string;
  earliestDate?: string;
  latestDate?: string;
  totalSnapshotDays?: number;
  totalTradingDays?: number;
  totalOptionRows?: number;
  healthScorePercent: number;
  corruptQuotesCount?: number;
  missingDates: string[];
  dailyAvailability?: { date: string; hasData: boolean; optionRows: number }[];
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
  createdAt?: string;
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
  apiKeyId?: string;
  consumerName: string;
  endpoint: string;
  httpMethod: string;
  statusCode: number;
  responseTimeMs: number;
  timestamp: string;
  ipAddress?: string;
}

export type PositionSizingMode = 'FixedContracts' | 'FixedDollarBudget' | 'PortfolioCompoundingPercent';

export interface BacktestRequest {
  strategy?: string;
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  
  // Position Sizing Methodology
  sizingMode?: PositionSizingMode;
  fixedContracts?: number;
  fixedDollarBudget?: number;
  allocationPercent?: number;

  // ITM Covered Call Risk Rules & Criteria
  minAnnualizedRocPercent?: number;
  minDownsideBufferPercent?: number;
  targetDelta: number;
  deltaTolerance?: number;
  targetDte: number;
  minDte: number;
  maxDte: number;
  profitTargetPercent?: number;
  stopLossPercent?: number;
  rollOnDeltaBreach: boolean;
  rollDeltaThreshold: number;
  closeDteThreshold?: number;
  commissionPerContract?: number;
  slippagePercent?: number;
  slippagePerContract?: number;
}

export interface BacktestTrade {
  id?: string;
  tradeNumber: number;
  entryDate: string;
  exitDate: string;
  holdDays: number;
  underlyingSymbol?: string;
  optionSymbol: string;
  strike: number;
  expirationDate?: string;
  entryDelta?: number;
  entryProbITM?: number;
  contracts: number;
  stockEntryPrice: number;
  optionEntryPrice: number;
  optionEntryPremium: number;
  stockExitPrice: number;
  optionExitPrice: number;
  optionExitPremium: number;
  netDebitPaid?: number;
  totalDebitOutlay?: number;
  netCreditReceived?: number;
  netPnL?: number;
  realizedPnlDollars?: number;
  returnOnCapitalPercent?: number;
  exitReason: ExitReason | string;
  notes?: string;
}

export interface EquityPoint {
  date: string;
  cash: number;
  stockValue: number;
  optionValue: number;
  totalEquity: number;
  drawdownPercent?: number;
  benchmarkPrice?: number;
  benchmarkEquity?: number;
  benchmarkReturnPercent?: number;
}

export type DailyEquityPoint = EquityPoint;

export interface PerformanceMetrics {
  initialCapital?: number;
  finalEquity: number;
  totalTrades: number;
  winningTrades: number;
  losingTrades: number;
  winRate: number;
  winRatePercent: number;
  totalNetProfit: number;
  totalReturnPercent: number;
  cagrPercent: number;
  benchmarkReturnPercent?: number;
  benchmarkCAGRPercent?: number;
  alphaPercent: number;
  sharpeRatio: number;
  sortinoRatio: number;
  maxDrawdownPercent: number;
  profitFactor: number;
  averageTradePnl?: number;
  averageWinningTradePnl?: number;
  averageLosingTradePnl?: number;
  averageHoldDays?: number;
  annualizedVolatility: number;
}

export interface BacktestResult {
  strategyName?: string;
  strategy?: string;
  symbol: string;
  startDate: string;
  endDate: string;
  initialCapital: number;
  parameters?: BacktestRequest;
  metrics: PerformanceMetrics;
  trades: BacktestTrade[];
  dailyEquityCurve: EquityPoint[];
}

// User & Auth Types
export interface UserProfile {
  id: string;
  email: string;
  name: string;
  pictureUrl?: string;
  role: string;
  isTwoFactorEnabled: boolean;
}

export interface AuthResponse {
  success: boolean;
  requiresTwoFactor: boolean;
  twoFactorChallengeToken?: string;
  token?: string;
  user?: UserProfile;
  message?: string;
}

export interface TwoFactorSetupResponse {
  secretKey: string;
  qrCodeUri: string;
  manualEntryKey?: string;
}

// System Diagnostics Types
export interface SystemHealthDto {
  status: string;
  timestamp: string;
  uptimeHours: number;
  memoryUsageMb: number;
  processorCount: number;
  database: {
    isConnected: boolean;
    pingLatencyMs: number;
    provider: string;
    totalWatchlistSymbols: number;
    totalOptionSnapshots: number;
  };
  tradierApi: {
    isConfigured: boolean;
    isOnline: boolean;
    latencyMs: number;
    statusDescription: string;
  };
  scheduler: {
    isRunning: boolean;
    dailyHarvestCron: string;
    integrityAuditCron: string;
  };
}

export interface SystemLogDto {
  id: string;
  timestamp: string;
  level: string;
  source: string;
  message: string;
  exception?: string;
}
