import React, { useState, useEffect } from 'react';
import { DiagnosticsApi } from '../services/api';
import { SystemHealthDto, SystemLogDto } from '../types';
import {
  Activity,
  Database,
  Cloud,
  Cpu,
  Clock,
  RefreshCw,
  AlertTriangle,
  CheckCircle2,
  XCircle,
  Search,
  Terminal,
  Zap,
  Radio
} from 'lucide-react';

export const TroubleshootingPage: React.FC = () => {
  const [health, setHealth] = useState<SystemHealthDto | null>(null);
  const [logs, setLogs] = useState<SystemLogDto[]>([]);
  const [selectedLevel, setSelectedLevel] = useState<string>('');
  const [searchTerm, setSearchTerm] = useState<string>('');
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [isTestingTradier, setIsTestingTradier] = useState<boolean>(false);
  const [tradierTestResult, setTradierTestResult] = useState<any>(null);
  const [autoRefresh, setAutoRefresh] = useState<boolean>(true);

  const fetchDiagnostics = async () => {
    try {
      const [hData, lData] = await Promise.all([
        DiagnosticsApi.getSystemHealth(),
        DiagnosticsApi.getLogs(selectedLevel || undefined, 100),
      ]);
      setHealth(hData);
      setLogs(lData);
    } catch (err) {
      console.error('Failed to load diagnostics:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchDiagnostics();
  }, [selectedLevel]);

  useEffect(() => {
    if (!autoRefresh) return;
    const interval = setInterval(fetchDiagnostics, 10000);
    return () => clearInterval(interval);
  }, [autoRefresh, selectedLevel]);

  const handleTestTradier = async () => {
    setIsTestingTradier(true);
    try {
      const res = await DiagnosticsApi.testTradier();
      setTradierTestResult(res);
      await fetchDiagnostics();
    } catch (err: any) {
      setTradierTestResult({
        isOnline: false,
        statusDescription: err?.message || 'Test request failed'
      });
    } finally {
      setIsTestingTradier(false);
    }
  };

  const filteredLogs = logs.filter(
    (l) =>
      l.message.toLowerCase().includes(searchTerm.toLowerCase()) ||
      l.source.toLowerCase().includes(searchTerm.toLowerCase())
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col md:flex-row md:items-center justify-between gap-4 bg-slate-900 border border-slate-800 p-6 rounded-2xl">
        <div className="flex items-center gap-3">
          <div className="p-3 bg-indigo-500/10 border border-indigo-500/30 rounded-xl text-indigo-400">
            <Activity className="w-6 h-6" />
          </div>
          <div>
            <h1 className="text-2xl font-bold text-white">System Diagnostics & Troubleshooting</h1>
            <p className="text-sm text-slate-400">
              Live operational health, latency metrics, cloud connector status, and structured error logs.
            </p>
          </div>
        </div>

        <div className="flex items-center gap-3">
          <button
            onClick={() => setAutoRefresh(!autoRefresh)}
            className={`flex items-center gap-2 px-3 py-1.5 rounded-xl border text-xs font-medium transition-colors ${
              autoRefresh
                ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400'
                : 'bg-slate-800 border-slate-700 text-slate-400'
            }`}
          >
            <Radio className={`w-3.5 h-3.5 ${autoRefresh ? 'animate-pulse' : ''}`} />
            {autoRefresh ? 'Auto-Refresh (10s)' : 'Auto-Refresh Paused'}
          </button>
          <button
            onClick={fetchDiagnostics}
            disabled={isLoading}
            className="flex items-center gap-2 px-4 py-2 bg-slate-800 hover:bg-slate-700 text-white text-xs font-medium rounded-xl border border-slate-700 transition-colors"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${isLoading ? 'animate-spin' : ''}`} />
            Refresh Now
          </button>
        </div>
      </div>

      {/* System Health Matrix */}
      {health && (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
          {/* Database Health */}
          <div className="bg-slate-900 border border-slate-800 p-5 rounded-2xl">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">PostgreSQL Database</span>
              <Database className="w-5 h-5 text-emerald-400" />
            </div>
            <div className="flex items-baseline gap-2">
              <span className="text-2xl font-bold text-white">
                {health.database.isConnected ? 'Connected' : 'Offline'}
              </span>
              <span className="text-xs font-mono text-emerald-400">{health.database.pingLatencyMs}ms</span>
            </div>
            <p className="text-xs text-slate-400 mt-2">
              {health.database.totalWatchlistSymbols} Symbols • {health.database.totalOptionSnapshots.toLocaleString()} Option Rows
            </p>
          </div>

          {/* Tradier API Status */}
          <div className="bg-slate-900 border border-slate-800 p-5 rounded-2xl">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Tradier Harvester</span>
              <Cloud className="w-5 h-5 text-sky-400" />
            </div>
            <div className="flex items-baseline gap-2">
              <span className="text-2xl font-bold text-white">
                {health.tradierApi.isOnline ? 'Active' : 'Unreachable'}
              </span>
              {health.tradierApi.latencyMs > 0 && (
                <span className="text-xs font-mono text-sky-400">{health.tradierApi.latencyMs}ms</span>
              )}
            </div>
            <p className="text-xs text-slate-400 mt-2 truncate">{health.tradierApi.statusDescription}</p>
          </div>

          {/* Memory & Process */}
          <div className="bg-slate-900 border border-slate-800 p-5 rounded-2xl">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Server Memory</span>
              <Cpu className="w-5 h-5 text-amber-400" />
            </div>
            <div className="flex items-baseline gap-2">
              <span className="text-2xl font-bold text-white">{health.memoryUsageMb} MB</span>
              <span className="text-xs text-slate-500">Working Set</span>
            </div>
            <p className="text-xs text-slate-400 mt-2">{health.processorCount} CPU Cores allocated</p>
          </div>

          {/* Process Uptime */}
          <div className="bg-slate-900 border border-slate-800 p-5 rounded-2xl">
            <div className="flex items-center justify-between mb-3">
              <span className="text-xs font-semibold text-slate-400 uppercase tracking-wider">Process Uptime</span>
              <Clock className="w-5 h-5 text-indigo-400" />
            </div>
            <div className="flex items-baseline gap-2">
              <span className="text-2xl font-bold text-white">{health.uptimeHours} hrs</span>
            </div>
            <p className="text-xs text-slate-400 mt-2">Quartz Scheduler Active</p>
          </div>
        </div>
      )}

      {/* Interactive Tradier Live Connection Tester */}
      <div className="bg-slate-900 border border-slate-800 p-6 rounded-2xl">
        <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-4">
          <div>
            <h2 className="text-lg font-bold text-white flex items-center gap-2">
              <Zap className="w-5 h-5 text-amber-400" />
              API Connectivity & Latency Tester
            </h2>
            <p className="text-xs text-slate-400 mt-0.5">
              Perform an immediate real-time roundtrip test to Tradier's market endpoints to verify token status and quotes.
            </p>
          </div>
          <button
            onClick={handleTestTradier}
            disabled={isTestingTradier}
            className="flex items-center gap-2 px-5 py-2.5 bg-amber-500 hover:bg-amber-400 disabled:opacity-50 text-slate-950 font-semibold text-xs rounded-xl shadow-lg shadow-amber-950/30 transition-colors"
          >
            {isTestingTradier ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Zap className="w-4 h-4" />}
            {isTestingTradier ? 'Pinging API...' : 'Test Tradier API Now'}
          </button>
        </div>

        {tradierTestResult && (
          <div
            className={`p-4 rounded-xl border flex items-start gap-3 mt-4 ${
              tradierTestResult.isOnline
                ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-300'
                : 'bg-rose-500/10 border-rose-500/30 text-rose-300'
            }`}
          >
            {tradierTestResult.isOnline ? (
              <CheckCircle2 className="w-5 h-5 shrink-0 text-emerald-400 mt-0.5" />
            ) : (
              <XCircle className="w-5 h-5 shrink-0 text-rose-400 mt-0.5" />
            )}
            <div>
              <p className="text-sm font-semibold text-white">
                {tradierTestResult.isOnline ? 'Tradier Connection Verified' : 'Tradier Connection Check Failed'}
              </p>
              <p className="text-xs mt-1">{tradierTestResult.statusDescription}</p>
              {tradierTestResult.latencyMs && (
                <p className="text-xs font-mono text-slate-400 mt-1">Roundtrip Latency: {tradierTestResult.latencyMs}ms</p>
              )}
            </div>
          </div>
        )}
      </div>

      {/* Live System Logs Stream */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl overflow-hidden shadow-xl">
        <div className="p-5 border-b border-slate-800 flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <Terminal className="w-5 h-5 text-indigo-400" />
            <h2 className="text-lg font-bold text-white">Live System Log Stream</h2>
            <span className="text-xs text-slate-500 ml-2">({filteredLogs.length} events)</span>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            {/* Level Filters */}
            <div className="flex items-center bg-slate-950 p-1 rounded-xl border border-slate-800">
              {['', 'Error', 'Warning', 'Information'].map((lvl) => (
                <button
                  key={lvl}
                  onClick={() => setSelectedLevel(lvl)}
                  className={`px-3 py-1 text-xs font-medium rounded-lg transition-colors ${
                    selectedLevel === lvl
                      ? 'bg-indigo-600 text-white'
                      : 'text-slate-400 hover:text-white'
                  }`}
                >
                  {lvl || 'All'}
                </button>
              ))}
            </div>

            {/* Search Input */}
            <div className="relative">
              <Search className="w-3.5 h-3.5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
              <input
                type="text"
                placeholder="Filter logs..."
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                className="bg-slate-950 border border-slate-800 rounded-xl py-1.5 pl-8 pr-3 text-xs text-white placeholder-slate-500 focus:outline-none focus:border-indigo-500 w-48"
              />
            </div>
          </div>
        </div>

        {/* Logs Table */}
        <div className="overflow-x-auto max-h-96">
          <table className="w-full text-left text-xs font-mono">
            <thead className="bg-slate-950/80 text-slate-400 border-b border-slate-800 sticky top-0 backdrop-blur-sm">
              <tr>
                <th className="py-2.5 px-4">Timestamp</th>
                <th className="py-2.5 px-4">Level</th>
                <th className="py-2.5 px-4">Source</th>
                <th className="py-2.5 px-4">Message</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {filteredLogs.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-8 text-center text-slate-500 font-sans text-xs">
                    No system log events match the selected criteria.
                  </td>
                </tr>
              ) : (
                filteredLogs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-2.5 px-4 text-slate-500 whitespace-nowrap">
                      {new Date(log.timestamp).toLocaleTimeString()}
                    </td>
                    <td className="py-2.5 px-4 whitespace-nowrap">
                      <span
                        className={`inline-block px-2 py-0.5 rounded text-[10px] font-semibold uppercase ${
                          log.level === 'Error'
                            ? 'bg-rose-500/20 text-rose-400'
                            : log.level === 'Warning'
                            ? 'bg-amber-500/20 text-amber-400'
                            : 'bg-emerald-500/20 text-emerald-400'
                        }`}
                      >
                        {log.level}
                      </span>
                    </td>
                    <td className="py-2.5 px-4 text-indigo-400 whitespace-nowrap font-medium">{log.source}</td>
                    <td className="py-2.5 px-4 text-slate-200">
                      <div>{log.message}</div>
                      {log.exception && (
                        <pre className="mt-1 text-[11px] text-rose-400/80 max-w-2xl truncate bg-slate-950 p-1.5 rounded">
                          {log.exception}
                        </pre>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
