import React, { useState, useEffect } from 'react';
import {
  Database,
  Plus,
  Play,
  Upload,
  RefreshCw,
  Trash2,
  CheckCircle2,
  XCircle,
  Clock,
  HardDrive,
  Layers,
  Sparkles,
  FileText
} from 'lucide-react';
import { MarketApi } from '../services/api';
import { WatchlistSymbol, DataHarvestJob, JobType } from '../types';

export const DashboardPage: React.FC = () => {
  const [watchlist, setWatchlist] = useState<WatchlistSymbol[]>([]);
  const [jobs, setJobs] = useState<DataHarvestJob[]>([]);
  const [loading, setLoading] = useState(true);

  // Modals state
  const [showAddModal, setShowAddModal] = useState(false);
  const [showSeedModal, setShowSeedModal] = useState(false);
  const [showCsvModal, setShowCsvModal] = useState(false);
  const [selectedJobLog, setSelectedJobLog] = useState<DataHarvestJob | null>(null);

  // Form states
  const [newSymbol, setNewSymbol] = useState('');
  const [newAssetType, setNewAssetType] = useState('Equity');
  const [seedSymbol, setSeedSymbol] = useState('AAPL');
  const [seedSource, setSeedSource] = useState<JobType>('DailyTradierHarvest');
  const [seedFrom, setSeedFrom] = useState('2025-01-01');
  const [seedTo, setSeedTo] = useState(new Date().toISOString().split('T')[0]);
  const [csvFile, setCsvFile] = useState<File | null>(null);
  const [csvFallbackSymbol, setCsvFallbackSymbol] = useState('');
  const [actionLoading, setActionLoading] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [wlData, jobsData] = await Promise.all([
        MarketApi.getWatchlist(),
        MarketApi.getHarvestJobs(20)
      ]);
      setWatchlist(wlData);
      setJobs(jobsData);
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const interval = setInterval(async () => {
      const recentJobs = await MarketApi.getHarvestJobs(20);
      setJobs(recentJobs);
    }, 5000);
    return () => clearInterval(interval);
  }, []);

  const handleToggleHarvest = async (symbol: string, current: boolean) => {
    try {
      await MarketApi.toggleHarvesting(symbol, !current);
      setWatchlist(prev =>
        prev.map(item => item.symbol === symbol ? { ...item, isActiveHarvesting: !current } : item)
      );
    } catch (err) {
      console.error('Failed to toggle harvest:', err);
    }
  };

  const handleDeleteSymbol = async (symbol: string) => {
    if (!confirm(`Are you sure you want to remove ${symbol} from the watchlist?`)) return;
    try {
      await MarketApi.deleteSymbol(symbol);
      setWatchlist(prev => prev.filter(item => item.symbol !== symbol));
    } catch (err) {
      console.error('Failed to delete symbol:', err);
    }
  };

  const handleAddSymbol = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!newSymbol.trim()) return;
    try {
      setActionLoading(true);
      await MarketApi.addSymbol({
        symbol: newSymbol.trim().toUpperCase(),
        assetType: newAssetType,
        isActiveHarvesting: true
      });
      setShowAddModal(false);
      setNewSymbol('');
      await loadData();
    } catch (err) {
      console.error('Failed to add symbol:', err);
    } finally {
      setActionLoading(false);
    }
  };

  const handleTriggerSeed = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setActionLoading(true);
      await MarketApi.triggerHarvest({
        symbol: seedSymbol,
        source: seedSource,
        from: seedFrom,
        to: seedTo
      });
      setShowSeedModal(false);
      await loadData();
    } catch (err) {
      console.error('Failed to seed:', err);
    } finally {
      setActionLoading(false);
    }
  };

  const handleUploadCsv = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!csvFile) return;
    try {
      setActionLoading(true);
      const fd = new FormData();
      fd.append('file', csvFile);
      await MarketApi.uploadCsv(fd, csvFallbackSymbol || undefined);
      setShowCsvModal(false);
      setCsvFile(null);
      await loadData();
    } catch (err) {
      console.error('CSV upload failed:', err);
    } finally {
      setActionLoading(false);
    }
  };

  const totalOptionRows = watchlist.reduce((acc, curr) => acc + curr.totalOptionRows, 0);
  const activeCount = watchlist.filter(w => w.isActiveHarvesting).length;
  const estimatedStorageMb = (totalOptionRows * 0.00035).toFixed(2);

  return (
    <div className="space-y-6">
      
      {/* Top Stat Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Tracked Assets</span>
            <Database className="w-4 h-4 text-blue-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2">{watchlist.length}</p>
          <p className="text-xs text-emerald-400 mt-1">? {activeCount} auto-harvesting</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Vaulted Option Rows</span>
            <Layers className="w-4 h-4 text-emerald-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2">
            {totalOptionRows.toLocaleString()}
          </p>
          <p className="text-xs text-slate-400 mt-1">EOD chains & Greeks</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">Vault Storage Size</span>
            <HardDrive className="w-4 h-4 text-purple-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2">{estimatedStorageMb} MB</p>
          <p className="text-xs text-purple-400 mt-1">PostgreSQL (TimescaleDB / Composite B-Tree)</p>
        </div>

        <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
          <div className="flex items-center justify-between">
            <span className="text-xs font-medium text-slate-400">EOD Brokerage Harvest</span>
            <Clock className="w-4 h-4 text-amber-400" />
          </div>
          <p className="text-2xl font-bold text-slate-100 mt-2">4:05 PM ET</p>
          <p className="text-xs text-emerald-400 mt-1">$0.00 Cost (Tradier Token)</p>
        </div>
      </div>

      {/* Watchlist Section */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 mb-5">
          <div>
            <h2 className="text-base font-bold text-slate-100">Watchlist & Market Data Vault</h2>
            <p className="text-xs text-slate-400">Tracked tickers for automated daily closing option chains and historical backtesting</p>
          </div>
          
          <div className="flex items-center space-x-2">
            <button
              onClick={() => setShowAddModal(true)}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium transition-all shadow-md shadow-blue-600/30"
            >
              <Plus className="w-3.5 h-3.5" />
              <span>Add Symbol</span>
            </button>
            <button
              onClick={() => setShowSeedModal(true)}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-indigo-600/20 hover:bg-indigo-600/30 border border-indigo-500/40 text-indigo-300 text-xs font-medium transition-all"
            >
              <Sparkles className="w-3.5 h-3.5" />
              <span>Trigger Historical Seed</span>
            </button>
            <button
              onClick={() => setShowCsvModal(true)}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium transition-all border border-slate-700"
            >
              <Upload className="w-3.5 h-3.5" />
              <span>Import CSV</span>
            </button>
          </div>
        </div>

        {/* Watchlist Table */}
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-800/60 text-slate-400 font-semibold uppercase tracking-wider border-b border-slate-800">
              <tr>
                <th className="py-3 px-4">Symbol</th>
                <th className="py-3 px-4">Type</th>
                <th className="py-3 px-4">Daily Harvest</th>
                <th className="py-3 px-4">Historical Range</th>
                <th className="py-3 px-4">Snapshot Days</th>
                <th className="py-3 px-4">Total Contracts</th>
                <th className="py-3 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {loading ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-slate-500">Loading market vault data...</td>
                </tr>
              ) : watchlist.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-8 text-center text-slate-500">No symbols tracked yet. Click "+ Add Symbol" to begin.</td>
                </tr>
              ) : (
                watchlist.map(item => (
                  <tr key={item.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-3 px-4 font-bold text-slate-100 font-mono text-sm">{item.symbol}</td>
                    <td className="py-3 px-4">
                      <span className="px-2 py-0.5 rounded bg-slate-800 text-slate-300 text-[11px] font-medium border border-slate-700">
                        {item.assetType}
                      </span>
                    </td>
                    <td className="py-3 px-4">
                      <button
                        onClick={() => handleToggleHarvest(item.symbol, item.isActiveHarvesting)}
                        className={`inline-flex items-center px-2.5 py-0.5 rounded-full text-[11px] font-medium transition-colors ${
                          item.isActiveHarvesting
                            ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/40 hover:bg-emerald-500/30'
                            : 'bg-slate-800 text-slate-400 border border-slate-700 hover:bg-slate-700'
                        }`}
                      >
                        {item.isActiveHarvesting ? 'Active (4:05 PM)' : 'Paused'}
                      </button>
                    </td>
                    <td className="py-3 px-4 font-mono text-slate-400 text-[11px]">
                      {item.earliestAvailableDate && item.latestAvailableDate
                        ? `${item.earliestAvailableDate} ? ${item.latestAvailableDate}`
                        : 'No data'}
                    </td>
                    <td className="py-3 px-4 font-mono">{item.totalSnapshotDays} days</td>
                    <td className="py-3 px-4 font-mono font-semibold text-emerald-400">
                      {item.totalOptionRows.toLocaleString()}
                    </td>
                    <td className="py-3 px-4 text-right space-x-2">
                      <button
                        onClick={() => {
                          setSeedSymbol(item.symbol);
                          setShowSeedModal(true);
                        }}
                        className="text-blue-400 hover:text-blue-300 text-xs"
                        title="Seed historical range"
                      >
                        Seed
                      </button>
                      <button
                        onClick={() => handleDeleteSymbol(item.symbol)}
                        className="text-rose-400 hover:text-rose-300 text-xs ml-2"
                        title="Remove ticker"
                      >
                        <Trash2 className="w-3.5 h-3.5 inline" />
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Harvester Jobs & Ingestion Logs */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-sm font-bold text-slate-100">Live Ingestion & Harvester Jobs</h3>
            <p className="text-xs text-slate-400">Real-time status of scheduled cron and background seed jobs</p>
          </div>
          <button
            onClick={loadData}
            className="flex items-center space-x-1 text-slate-400 hover:text-slate-200 text-xs px-2.5 py-1 rounded bg-slate-800 border border-slate-700"
          >
            <RefreshCw className="w-3 h-3" />
            <span>Refresh</span>
          </button>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-800/60 text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-800">
              <tr>
                <th className="py-2.5 px-3">Job Type</th>
                <th className="py-2.5 px-3">Target</th>
                <th className="py-2.5 px-3">Status</th>
                <th className="py-2.5 px-3">Rows Inserted</th>
                <th className="py-2.5 px-3">Started</th>
                <th className="py-2.5 px-3 text-right">Logs</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 text-slate-300">
              {jobs.slice(0, 10).map(job => (
                <tr key={job.id} className="hover:bg-slate-800/30">
                  <td className="py-2.5 px-3 font-medium text-slate-200">{job.jobType}</td>
                  <td className="py-2.5 px-3 font-mono text-slate-400">
                    {job.symbol ? `${job.symbol} (${job.targetDateRange || 'EOD'})` : 'All Active'}
                  </td>
                  <td className="py-2.5 px-3">
                    <span className={`inline-flex items-center space-x-1 px-2 py-0.5 rounded-full text-[11px] font-medium ${
                      job.status === 'Completed' ? 'bg-emerald-500/20 text-emerald-400 border border-emerald-500/30' :
                      job.status === 'Running' ? 'bg-blue-500/20 text-blue-400 border border-blue-500/30 animate-pulse' :
                      job.status === 'Failed' ? 'bg-rose-500/20 text-rose-400 border border-rose-500/30' :
                      'bg-slate-800 text-slate-400'
                    }`}>
                      {job.status === 'Completed' && <CheckCircle2 className="w-3 h-3 inline mr-1" />}
                      {job.status === 'Failed' && <XCircle className="w-3 h-3 inline mr-1" />}
                      {job.status}
                    </span>
                  </td>
                  <td className="py-2.5 px-3 font-mono font-semibold text-emerald-400">
                    {job.rowsInserted.toLocaleString()}
                  </td>
                  <td className="py-2.5 px-3 text-slate-400">
                    {job.startedAt ? new Date(job.startedAt).toLocaleTimeString() : '-'}
                  </td>
                  <td className="py-2.5 px-3 text-right">
                    <button
                      onClick={() => setSelectedJobLog(job)}
                      className="text-xs text-blue-400 hover:text-blue-300 underline inline-flex items-center space-x-1"
                    >
                      <FileText className="w-3 h-3" />
                      <span>Details</span>
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal: Add Symbol */}
      {showAddModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-xl max-w-md w-full p-6 shadow-2xl">
            <h3 className="text-base font-bold text-slate-100 mb-4">Add Symbol to Watchlist</h3>
            <form onSubmit={handleAddSymbol} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Ticker Symbol</label>
                <input
                  type="text"
                  value={newSymbol}
                  onChange={e => setNewSymbol(e.target.value)}
                  placeholder="e.g. AAPL, SPY, NVDA"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:border-blue-500"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Asset Type</label>
                <select
                  value={newAssetType}
                  onChange={e => setNewAssetType(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:border-blue-500"
                >
                  <option value="Equity">Equity (Stock)</option>
                  <option value="ETF">ETF</option>
                  <option value="Index">Index</option>
                </select>
              </div>

              <div className="flex justify-end space-x-2 pt-3">
                <button
                  type="button"
                  onClick={() => setShowAddModal(false)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading}
                  className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium disabled:opacity-50"
                >
                  {actionLoading ? 'Adding...' : 'Add Symbol'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Trigger Seed */}
      {showSeedModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-xl max-w-md w-full p-6 shadow-2xl">
            <h3 className="text-base font-bold text-slate-100 mb-2">Trigger Historical Seeder</h3>
            <p className="text-xs text-slate-400 mb-4">Ingest historical options chains and stock candles into the local vault</p>
            
            <form onSubmit={handleTriggerSeed} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Symbol</label>
                <input
                  type="text"
                  value={seedSymbol}
                  onChange={e => setSeedSymbol(e.target.value)}
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:border-blue-500 font-mono"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Ingestion Source Driver</label>
                <select
                  value={seedSource}
                  onChange={e => setSeedSource(e.target.value as JobType)}
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none focus:border-blue-500"
                >
                  <option value="DailyTradierHarvest">Tradier EOD Driver ($0 cost)</option>
                  <option value="ThetaDataSeed">ThetaData Historical Bridge</option>
                  <option value="MarketDataSeed">MarketData.app (Filtered Smart-Query)</option>
                </select>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">From Date</label>
                  <input
                    type="date"
                    value={seedFrom}
                    onChange={e => setSeedFrom(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-xs text-white focus:outline-none"
                    required
                  />
                </div>
                <div>
                  <label className="block text-xs font-medium text-slate-400 mb-1">To Date</label>
                  <input
                    type="date"
                    value={seedTo}
                    onChange={e => setSeedTo(e.target.value)}
                    className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-xs text-white focus:outline-none"
                    required
                  />
                </div>
              </div>

              <div className="flex justify-end space-x-2 pt-3">
                <button
                  type="button"
                  onClick={() => setShowSeedModal(false)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading}
                  className="px-4 py-2 rounded-lg bg-indigo-600 hover:bg-indigo-500 text-white text-xs font-medium disabled:opacity-50"
                >
                  {actionLoading ? 'Starting Seed...' : 'Start Ingestion'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Bulk CSV */}
      {showCsvModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-xl max-w-md w-full p-6 shadow-2xl">
            <h3 className="text-base font-bold text-slate-100 mb-2">Import Flat File / CBOE CSV</h3>
            <p className="text-xs text-slate-400 mb-4">Auto-detects columns: OCC Symbol, Strike, Expiration, Greeks, IV, Bid/Ask</p>
            
            <form onSubmit={handleUploadCsv} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">CSV File</label>
                <input
                  type="file"
                  accept=".csv,.txt"
                  onChange={e => setCsvFile(e.target.files?.[0] || null)}
                  className="w-full text-xs text-slate-300 file:mr-4 file:py-2 file:px-4 file:rounded-lg file:border-0 file:text-xs file:font-semibold file:bg-blue-600 file:text-white hover:file:bg-blue-500"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Fallback Symbol (Optional)</label>
                <input
                  type="text"
                  value={csvFallbackSymbol}
                  onChange={e => setCsvFallbackSymbol(e.target.value)}
                  placeholder="e.g. AAPL"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none"
                />
              </div>

              <div className="flex justify-end space-x-2 pt-3">
                <button
                  type="button"
                  onClick={() => setShowCsvModal(false)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={actionLoading || !csvFile}
                  className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium disabled:opacity-50"
                >
                  {actionLoading ? 'Uploading...' : 'Import Dataset'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      {/* Modal: Job Details Log */}
      {selectedJobLog && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-xl max-w-2xl w-full p-6 shadow-2xl">
            <div className="flex items-center justify-between mb-3">
              <h3 className="text-base font-bold text-slate-100">Job Execution Log</h3>
              <button onClick={() => setSelectedJobLog(null)} className="text-slate-400 hover:text-white">?</button>
            </div>
            
            <div className="grid grid-cols-3 gap-2 text-xs mb-3 text-slate-400">
              <div>Type: <span className="text-slate-200">{selectedJobLog.jobType}</span></div>
              <div>Status: <span className="text-emerald-400">{selectedJobLog.status}</span></div>
              <div>Rows: <span className="text-slate-200 font-mono">{selectedJobLog.rowsInserted}</span></div>
            </div>

            <div className="bg-black/80 rounded-lg p-3 font-mono text-xs text-emerald-400 h-64 overflow-y-auto whitespace-pre-wrap border border-slate-800">
              {selectedJobLog.executionLog || 'No log details available.'}
            </div>

            <div className="flex justify-end mt-4">
              <button
                onClick={() => setSelectedJobLog(null)}
                className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-200 text-xs font-medium"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}

    </div>
  );
};
