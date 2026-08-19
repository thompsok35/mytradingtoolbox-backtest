import React, { useState, useEffect } from 'react';
import { ShieldCheck, AlertTriangle, CheckCircle, Wrench, RefreshCw, Activity, Calendar } from 'lucide-react';
import { MarketApi } from '../services/api';
import { MarketCoverageDto, DataIntegrityAudit, WatchlistSymbol } from '../types';
import { HeatmapCalendar } from '../components/HeatmapCalendar';

export const DataIntegrityPage: React.FC = () => {
  const [symbols, setSymbols] = useState<WatchlistSymbol[]>([]);
  const [selectedSymbol, setSelectedSymbol] = useState('AAPL');
  const [coverage, setCoverage] = useState<MarketCoverageDto | null>(null);
  const [audits, setAudits] = useState<DataIntegrityAudit[]>([]);
  const [availableDates, setAvailableDates] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);
  const [repairing, setRepairing] = useState(false);

  const loadInitialData = async () => {
    try {
      setLoading(true);
      const [wl, allAudits] = await Promise.all([
        MarketApi.getWatchlist(),
        MarketApi.getAllAudits()
      ]);
      setSymbols(wl);
      setAudits(allAudits);
      if (wl.length > 0) {
        setSelectedSymbol(wl[0].symbol);
      }
    } catch (err) {
      console.error('Failed to load integrity audits:', err);
    } finally {
      setLoading(false);
    }
  };

  const loadSymbolCoverage = async (sym: string) => {
    try {
      setLoading(true);
      const cov = await MarketApi.getCoverage(sym);
      setCoverage(cov);

      // Fetch available dates from chain/candles
      const candles = await MarketApi.getStockCandles(sym);
      setAvailableDates(candles.map(c => c.date));
    } catch (err) {
      console.error('Failed to get coverage:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadInitialData();
  }, []);

  useEffect(() => {
    if (selectedSymbol) {
      loadSymbolCoverage(selectedSymbol);
    }
  }, [selectedSymbol]);

  const handleRunAudit = async () => {
    try {
      setLoading(true);
      await MarketApi.auditSymbol(selectedSymbol);
      await loadSymbolCoverage(selectedSymbol);
      const allAudits = await MarketApi.getAllAudits();
      setAudits(allAudits);
    } catch (err) {
      console.error('Failed to run audit:', err);
    } finally {
      setLoading(false);
    }
  };

  const handleAutoRepair = async () => {
    try {
      setRepairing(true);
      await MarketApi.repairSymbolGaps(selectedSymbol);
      await loadSymbolCoverage(selectedSymbol);
      const allAudits = await MarketApi.getAllAudits();
      setAudits(allAudits);
    } catch (err) {
      console.error('Auto-repair failed:', err);
    } finally {
      setRepairing(false);
    }
  };

  const health = coverage?.healthScorePercent ?? 100;

  return (
    <div className="space-y-6">
      
      {/* Top Banner */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
        <div>
          <h2 className="text-base font-bold text-slate-100 flex items-center space-x-2">
            <ShieldCheck className="w-5 h-5 text-emerald-400" />
            <span>Data Quality & Integrity Auditor</span>
          </h2>
          <p className="text-xs text-slate-400">
            Continuously validates calendar completeness, identifies missing trading days, and auto-repairs data gaps
          </p>
        </div>

        <div className="flex items-center space-x-3">
          <select
            value={selectedSymbol}
            onChange={e => setSelectedSymbol(e.target.value)}
            className="px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-xs font-mono font-bold text-blue-400 focus:outline-none"
          >
            {symbols.map(s => (
              <option key={s.id} value={s.symbol}>{s.symbol}</option>
            ))}
          </select>

          <button
            onClick={handleRunAudit}
            disabled={loading}
            className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700 transition-all"
          >
            <RefreshCw className={`w-3.5 h-3.5 ${loading ? 'animate-spin' : ''}`} />
            <span>Scan Now</span>
          </button>

          <button
            onClick={handleAutoRepair}
            disabled={repairing || (coverage?.missingDates.length === 0 && coverage?.corruptQuotesCount === 0)}
            className="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-medium shadow-md shadow-emerald-600/30 transition-all disabled:opacity-50"
          >
            <Wrench className={`w-3.5 h-3.5 ${repairing ? 'animate-spin' : ''}`} />
            <span>{repairing ? 'Repairing Gaps...' : '1-Click Auto-Repair'}</span>
          </button>
        </div>
      </div>

      {/* Metrics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Health Score</span>
            <Activity className="w-4 h-4 text-emerald-400" />
          </div>
          <p className="text-2xl font-bold text-emerald-400 mt-2 font-mono">{health.toFixed(1)}%</p>
          <p className="text-xs text-slate-400 mt-1">Calendar completeness</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Trading Days Vaulted</span>
            <CheckCircle className="w-4 h-4 text-blue-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2 font-mono">{coverage?.totalSnapshotDays ?? 0}</p>
          <p className="text-xs text-slate-400 mt-1">Sessions available</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Missing Session Gaps</span>
            <AlertTriangle className="w-4 h-4 text-rose-400" />
          </div>
          <p className="text-2xl font-bold text-rose-400 mt-2 font-mono">{coverage?.missingDates.length ?? 0}</p>
          <p className="text-xs text-rose-400/80 mt-1">Requiring backfill</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Corrupt Quotes</span>
            <ShieldCheck className="w-4 h-4 text-amber-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2 font-mono">{coverage?.corruptQuotesCount ?? 0}</p>
          <p className="text-xs text-slate-400 mt-1">Inverted bid/ask or missing Greeks</p>
        </div>
      </div>

      {/* Contribution Calendar Heatmap */}
      <HeatmapCalendar
        availableDates={availableDates}
        missingDates={coverage?.missingDates || []}
        totalExpectedDays={coverage?.totalSnapshotDays || 0}
        healthScore={health}
      />

      {/* All Symbols Health Overview Table */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <h3 className="text-sm font-bold text-slate-100 mb-4">Tracked Assets Health Scores</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-800/60 text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-800">
              <tr>
                <th className="py-2.5 px-3">Symbol</th>
                <th className="py-2.5 px-3">Health Score</th>
                <th className="py-2.5 px-3">Actual Sessions</th>
                <th className="py-2.5 px-3">Expected Sessions</th>
                <th className="py-2.5 px-3">Corrupt Quotes</th>
                <th className="py-2.5 px-3 text-right">Action</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {audits.map(audit => (
                <tr key={audit.id} className="hover:bg-slate-800/30">
                  <td className="py-2.5 px-3 font-mono font-bold text-slate-100">{audit.symbol}</td>
                  <td className="py-2.5 px-3">
                    <span className={`px-2 py-0.5 rounded text-xs font-mono font-semibold ${
                      audit.healthScorePercent >= 95 ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' :
                      audit.healthScorePercent >= 80 ? 'bg-amber-500/20 text-amber-400 border border-amber-500/30' :
                      'bg-rose-500/20 text-rose-400 border border-rose-500/30'
                    }`}>
                      {audit.healthScorePercent.toFixed(1)}%
                    </span>
                  </td>
                  <td className="py-2.5 px-3 font-mono">{audit.actualDaysPresent}</td>
                  <td className="py-2.5 px-3 font-mono">{audit.totalExpectedTradingDays}</td>
                  <td className="py-2.5 px-3 font-mono text-slate-400">{audit.corruptQuotesCount}</td>
                  <td className="py-2.5 px-3 text-right">
                    <button
                      onClick={() => setSelectedSymbol(audit.symbol)}
                      className="text-blue-400 hover:text-blue-300 text-xs"
                    >
                      Inspect
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

    </div>
  );
};
