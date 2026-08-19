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
  JobType,
  UserProfile,
  AuthResponse,
  TwoFactorSetupResponse,
  SystemHealthDto,
  SystemLogDto
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

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('mtt_jwt_token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

export const AuthApi = {
  getConfig: async () =>
    (await api.get<{ googleClientId: string }>('/auth/config')).data,
  loginWithGoogle: async (credential: string) =>
    (await api.post<AuthResponse>('/auth/google', { credential })).data,
  verifyTwoFactor: async (payload: { twoFactorChallengeToken?: string; code: string }) =>
    (await api.post<AuthResponse>('/auth/2fa/verify', payload)).data,
  setupTwoFactor: async () =>
    (await api.post<TwoFactorSetupResponse>('/auth/2fa/setup')).data,
  disableTwoFactor: async () =>
    (await api.post<{ success: boolean; message: string }>('/auth/2fa/disable')).data,
  getCurrentUser: async () =>
    (await api.get<UserProfile>('/auth/me')).data,
};

export const DiagnosticsApi = {
  getSystemHealth: async () =>
    (await api.get<SystemHealthDto>('/diagnostics/system-health')).data,
  testTradier: async () =>
    (await api.post<SystemHealthDto['tradierApi']>('/diagnostics/test-tradier')).data,
  getLogs: async (level?: string, limit: number = 100) =>
    (await api.get<SystemLogDto[]>('/diagnostics/logs', { params: { level, limit } })).data,
};

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
  getStockCandles: async (symbol: string, from?: string, to?: string) =>
    (await api.get<StockCandleDto[]>(`/stocks/candles/${symbol}`, { params: { from, to } })).data,

  // Harvester & Ingestion
  triggerSeed: async (payload: { symbol: string; source: JobType; fromDate: string; toDate: string }) =>
    (await api.post<DataHarvestJob>('/harvester/trigger', null, { params: { symbol: payload.symbol, source: payload.source, from: payload.fromDate, to: payload.toDate } })).data,
  triggerHarvest: async (
    arg1: string | { symbol: string; source: JobType; from?: string; to?: string },
    source?: JobType,
    fromDate?: string,
    toDate?: string
  ) => {
    if (typeof arg1 === 'object') {
      return (await api.post<DataHarvestJob>('/harvester/trigger', null, {
        params: { symbol: arg1.symbol, source: arg1.source, from: arg1.from, to: arg1.to }
      })).data;
    }
    return (await api.post<DataHarvestJob>('/harvester/trigger', null, {
      params: { symbol: arg1, source, from: fromDate, to: toDate }
    })).data;
  },
  triggerDailyHarvest: async () => (await api.post<DataHarvestJob>('/harvester/run-daily')).data,
  getHarvestJobs: async (limit: number = 20) =>
    (await api.get<DataHarvestJob[]>('/harvester/jobs', { params: { limit } })).data,
  uploadCsv: async (fileOrFormData: File | FormData, _symbol?: string) => {
    let formData: FormData;
    if (fileOrFormData instanceof FormData) {
      formData = fileOrFormData;
    } else {
      formData = new FormData();
      formData.append('file', fileOrFormData);
    }
    return (await api.post<{ snapshotsInserted: number; candlesInserted: number; report: string }>('/harvester/upload-csv', formData)).data;
  },

  // Integrity
  getIntegrityAudit: async (symbol: string) =>
    (await api.get<DataIntegrityAudit>(`/integrity/audit/${symbol}`)).data,
  auditSymbol: async (symbol: string) =>
    (await api.post<DataIntegrityAudit>(`/integrity/audit/${symbol}`)).data,
  getAllAudits: async () => {
    try {
      return (await api.get<DataIntegrityAudit[]>('/integrity/audits')).data;
    } catch {
      return [];
    }
  },
  repairGaps: async (symbol: string) =>
    (await api.post<DataIntegrityAudit>(`/integrity/repair/${symbol}`)).data,
  repairSymbolGaps: async (symbol: string) =>
    (await api.post<DataIntegrityAudit>(`/integrity/repair/${symbol}`)).data,

  // Backtest
  executeBacktest: async (request: BacktestRequest) =>
    (await api.post<BacktestResult>('/backtest/execute', request)).data,

  // API Keys
  getApiKeys: async () => (await api.get<ApiKey[]>('/auth/keys')).data,
  createApiKey: async (consumerName: string | { consumerName: string; rateLimitPerMinute?: number }, rateLimit?: number) => {
    const payload = typeof consumerName === 'string'
      ? { consumerName, rateLimitPerMinute: rateLimit || 120 }
      : { consumerName: consumerName.consumerName, rateLimitPerMinute: consumerName.rateLimitPerMinute || 120 };
    return (await api.post<ApiKey>('/auth/keys', payload)).data;
  },
  generateApiKey: async (payload: { consumerName: string; rateLimitPerMinute: number; expiresAt?: string }) =>
    (await api.post<ApiKey>('/auth/keys', payload)).data,
  revokeApiKey: async (id: string) => (await api.delete(`/auth/keys/${id}`)).data,
  getApiLogs: async (limit: number = 50) => (await api.get<ApiUsageLog[]>('/auth/logs', { params: { limit } })).data,
};
