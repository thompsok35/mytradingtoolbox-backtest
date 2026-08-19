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

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api/v1',
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
    (await api.get(`/options/quotes/${optionSymbol}`, { params: { from, to } })).data,

  // Stock Candles
  getStockCandles: async (symbol: string, from?: string, to?: string) =>
    (await api.get<StockCandleDto[]>(`/stocks/candles/${symbol}`, { params: { from, to } })).data,

  // Harvester & Seeder
  triggerHarvest: async (params: { symbol: string; source: JobType; from?: string; to?: string }) =>
    (await api.post<DataHarvestJob>('/harvester/trigger', null, { params })).data,
  runDailyHarvest: async () => (await api.post<DataHarvestJob>('/harvester/run-daily')).data,
  getHarvestJobs: async (count = 50) => (await api.get<DataHarvestJob[]>('/harvester/jobs', { params: { count } })).data,
  uploadCsv: async (formData: FormData, fallbackSymbol?: string) =>
    (await api.post('/harvester/upload-csv', formData, {
      params: { fallbackSymbol },
      headers: { 'Content-Type': 'multipart/form-data' }
    })).data,

  // Data Integrity & Repair
  auditSymbol: async (symbol: string) => (await api.post<DataIntegrityAudit>(`/integrity/audit/${symbol}`)).data,
  repairSymbolGaps: async (symbol: string) => (await api.post<DataHarvestJob>(`/integrity/repair/${symbol}`)).data,
  getAllAudits: async () => (await api.get<DataIntegrityAudit[]>('/integrity/audits')).data,

  // Backtest
  executeBacktest: async (request: BacktestRequest) =>
    (await api.post<BacktestResult>('/backtest/execute', request)).data,

  // API Keys & Usage Logs
  getApiKeys: async () => (await api.get<ApiKey[]>('/auth/keys')).data,
  createApiKey: async (consumerName: string, rateLimitPerMinute = 120, expiresAt?: string) =>
    (await api.post<ApiKey>('/auth/keys', { consumerName, rateLimitPerMinute, expiresAt })).data,
  revokeApiKey: async (id: string) => (await api.delete(`/auth/keys/${id}`)).data,
  getApiLogs: async (count = 100) => (await api.get<ApiUsageLog[]>('/auth/logs', { params: { count } })).data,
};
