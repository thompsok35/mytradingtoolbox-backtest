import axios from 'axios';
import {
  WatchlistSymbol,
  OptionChainResponseDto,
  StockCandleDto,
  MarketCoverageDto,
  DataHarvestJob,
  DataIntegrityAudit,
  ApiKey,
  ApiUsageLog,
  BacktestRequest,
  BacktestResult,
  JobType
} from '../types';

const resolveBaseUrl = () => {
  if (import.meta.env.VITE_API_URL) {
    return import.meta.env.VITE_API_URL;
  }
  if (typeof window !== 'undefined' && window.location.hostname.includes('mytradingtoolbox.com')) {
    return 'https://api.backtest.mytradingtoolbox.com/api/v1';
  }
  return '/api/v1';
};

const api = axios.create({
  baseURL: resolveBaseUrl(),
});

export const MarketApi = {
  // Watchlist & Coverage
  getWatchlist: async () => (await api.get<WatchlistSymbol[]>('/market/watchlist')).data,
  addSymbol: async (symbol: { symbol: string; assetType: string; isActiveHarvesting: boolean }) =>
    (await api.post<WatchlistSymbol>('/market/watchlist', symbol)).data,
  toggleHarvesting: async (symbol: string, active: boolean) =>
    (await api.put(`/market/watchlist/${symbol}/toggle?active=${active}`)).data,
  deleteSymbol: async (symbol: string) => (await api.delete(`/market/watchlist/${symbol}`)).data,
  getCoverage: async (symbol: string) => (await api.get<MarketCoverageDto>(`/market/coverage/${symbol}`)).data,

  // Options & Chains
  getOptionChain: async (params: {
    symbol: string;
    date?: string;
    minDte?: number;
    maxDte?: number;
    minStrike?: number;
    maxStrike?: number;
    side?: string;
  }) => (await api.get<OptionChainResponseDto>(`/options/chain/${params.symbol}`, { params })).data,
  getOptionQuotes: async (optionSymbol: string, from?: string, to?: string) =>
    (await api.get<OptionChainResponseDto>(`/options/quotes/${optionSymbol}`, { params: { from, to } })).data,

  // Stocks
  getStockCandles: async (symbol: string, from: string, to: string) =>
    (await api.get<StockCandleDto[]>(`/stocks/candles/${symbol}`, { params: { from, to } })).data,

  // Harvester & Ingestion
  triggerSeed: async (payload: { symbol: string; source: JobType; fromDate: string; toDate: string }) =>
    (await api.post<DataHarvestJob>('/harvester/trigger', payload)).data,
  triggerDailyHarvest: async () => (await api.post<DataHarvestJob>('/harvester/run-daily')).data,
  getHarvestJobs: async (limit: number = 20) =>
    (await api.get<DataHarvestJob[]>('/harvester/jobs', { params: { limit } })).data,
  uploadCsv: async (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return (await api.post<{ snapshotsInserted: number; candlesInserted: number; report: string }>('/harvester/upload-csv', formData)).data;
  },

  // Integrity
  getIntegrityAudit: async (symbol: string) =>
    (await api.get<DataIntegrityAudit>(`/integrity/audit/${symbol}`)).data,
  repairGaps: async (symbol: string) =>
    (await api.post<DataIntegrityAudit>(`/integrity/repair/${symbol}`)).data,

  // Backtest
  executeBacktest: async (request: BacktestRequest) =>
    (await api.post<BacktestResult>('/backtest/execute', request)).data,

  // API Keys
  getApiKeys: async () => (await api.get<ApiKey[]>('/auth/keys')).data,
  generateApiKey: async (payload: { consumerName: string; rateLimitPerMinute: number; expiresAt?: string }) =>
    (await api.post<ApiKey>('/auth/keys', payload)).data,
  revokeApiKey: async (id: string) => (await api.delete(`/auth/keys/${id}`)).data,
  getApiLogs: async (limit: number = 50) => (await api.get<ApiUsageLog[]>('/auth/logs', { params: { limit } })).data,
};
