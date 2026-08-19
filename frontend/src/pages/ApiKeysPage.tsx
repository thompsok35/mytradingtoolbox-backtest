import React, { useState, useEffect } from 'react';
import { Key, Plus, Copy, Trash2, Check, ExternalLink, Code2, Activity, Shield } from 'lucide-react';
import { MarketApi } from '../services/api';
import { ApiKey, ApiUsageLog } from '../types';

export const ApiKeysPage: React.FC = () => {
  const [keys, setKeys] = useState<ApiKey[]>([]);
  const [logs, setLogs] = useState<ApiUsageLog[]>([]);
  const [loading, setLoading] = useState(false);
  const [copiedKey, setCopiedKey] = useState<string | null>(null);

  // New Key Form
  const [consumerName, setConsumerName] = useState('');
  const [rateLimit, setRateLimit] = useState(180);
  const [showCreateModal, setShowCreateModal] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [k, l] = await Promise.all([
        MarketApi.getApiKeys(),
        MarketApi.getApiLogs(50)
      ]);
      setKeys(k);
      setLogs(l);
    } catch (err) {
      console.error('Failed to load api keys/logs:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const interval = setInterval(async () => {
      const recentLogs = await MarketApi.getApiLogs(50);
      setLogs(recentLogs);
    }, 5000);
    return () => clearInterval(interval);
  }, []);

  const handleCreateKey = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!consumerName.trim()) return;
    try {
      await MarketApi.createApiKey(consumerName.trim(), rateLimit);
      setConsumerName('');
      setShowCreateModal(false);
      await loadData();
    } catch (err) {
      console.error('Failed to create key:', err);
    }
  };

  const handleRevoke = async (id: string) => {
    if (!confirm('Are you sure you want to revoke this API key?')) return;
    try {
      await MarketApi.revokeApiKey(id);
      await loadData();
    } catch (err) {
      console.error('Failed to revoke key:', err);
    }
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedKey(text);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  const sampleKey = keys.length > 0 ? keys[0].key : 'mtt_sample_token_12345';

  return (
    <div className="space-y-6">
      
      {/* Top Header */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-col md:flex-row items-start md:items-center justify-between gap-4">
        <div>
          <h2 className="text-base font-bold text-slate-100 flex items-center space-x-2">
            <Key className="w-5 h-5 text-indigo-400" />
            <span>API Keys & Consumer Application Access</span>
          </h2>
          <p className="text-xs text-slate-400">
            Generate and manage high-performance Bearer access tokens for itmCCbot, Market Insights, and strategy scanners
          </p>
        </div>

        <div className="flex items-center space-x-3">
          <a
            href="http://localhost:5000/swagger"
            target="_blank"
            rel="noreferrer"
            className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium border border-slate-700 transition-all"
          >
            <span>OpenAPI Docs</span>
            <ExternalLink className="w-3.5 h-3.5" />
          </a>
          <button
            onClick={() => setShowCreateModal(true)}
            className="flex items-center space-x-1.5 px-3.5 py-1.5 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium shadow-md shadow-blue-600/30 transition-all"
          >
            <Plus className="w-3.5 h-3.5" />
            <span>Generate Token</span>
          </button>
        </div>
      </div>

      {/* Keys List */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <h3 className="text-sm font-bold text-slate-100 mb-4">Active Application Tokens</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-800/60 text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-800">
              <tr>
                <th className="py-2.5 px-3">Consumer App</th>
                <th className="py-2.5 px-3">Bearer Token</th>
                <th className="py-2.5 px-3">Status</th>
                <th className="py-2.5 px-3">Rate Limit</th>
                <th className="py-2.5 px-3">Total Requests</th>
                <th className="py-2.5 px-3">Created</th>
                <th className="py-2.5 px-3 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
              {keys.map(k => (
                <tr key={k.id} className="hover:bg-slate-800/30">
                  <td className="py-2.5 px-3 font-sans font-bold text-slate-100">{k.consumerName}</td>
                  <td className="py-2.5 px-3">
                    <div className="flex items-center space-x-2">
                      <span className="text-blue-400 bg-slate-800 px-2 py-0.5 rounded text-[11px]">
                        {k.key.substring(0, 16)}...
                      </span>
                      <button
                        onClick={() => copyToClipboard(k.key)}
                        className="text-slate-400 hover:text-white"
                        title="Copy full key"
                      >
                        {copiedKey === k.key ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                      </button>
                    </div>
                  </td>
                  <td className="py-2.5 px-3 font-sans">
                    <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                      k.isActive ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'
                    }`}>
                      {k.isActive ? 'Active' : 'Revoked'}
                    </span>
                  </td>
                  <td className="py-2.5 px-3">{k.rateLimitPerMinute} req/min</td>
                  <td className="py-2.5 px-3 font-bold text-emerald-400">{k.totalRequests.toLocaleString()}</td>
                  <td className="py-2.5 px-3 text-slate-400 text-[11px]">{new Date(k.createdAt).toLocaleDateString()}</td>
                  <td className="py-2.5 px-3 text-right">
                    {k.isActive && (
                      <button
                        onClick={() => handleRevoke(k.id)}
                        className="text-rose-400 hover:text-rose-300 text-xs font-sans"
                      >
                        <Trash2 className="w-3.5 h-3.5 inline" />
                      </button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Integration Code Snippets */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <div className="flex items-center space-x-2 text-xs font-bold text-slate-200 uppercase tracking-wider mb-3">
          <Code2 className="w-4 h-4 text-blue-400" />
          <span>Consumer Integration Snippets (cURL & REST)</span>
        </div>

        <div className="space-y-3">
          <div>
            <span className="text-[11px] text-slate-400">1. Fetch Option Chain with Greeks (itmCCbot):</span>
            <pre className="bg-black/80 rounded-lg p-3 text-xs font-mono text-emerald-400 overflow-x-auto border border-slate-800 mt-1">
{`curl -X GET "http://localhost:5000/api/v1/options/chain/AAPL?date=2025-06-15&minDte=20&maxDte=45" \\
  -H "Authorization: Bearer ${sampleKey}"`}
            </pre>
          </div>

          <div>
            <span className="text-[11px] text-slate-400">2. Execute Server-Side Backtest:</span>
            <pre className="bg-black/80 rounded-lg p-3 text-xs font-mono text-emerald-400 overflow-x-auto border border-slate-800 mt-1">
{`curl -X POST "http://localhost:5000/api/v1/backtest/execute" \\
  -H "Authorization: Bearer ${sampleKey}" \\
  -H "Content-Type: application/json" \\
  -d '{"symbol":"AAPL","startDate":"2025-01-01","endDate":"2025-06-30","initialCapital":50000,"targetDelta":0.70}'`}
            </pre>
          </div>
        </div>
      </div>

      {/* Live Consumer Request Logs */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <div className="flex items-center justify-between mb-4">
          <div>
            <h3 className="text-sm font-bold text-slate-100">Live Consumer App Request Logs</h3>
            <p className="text-xs text-slate-400">Real-time latency and status codes from consumer services</p>
          </div>
          <span className="text-xs text-emerald-400 flex items-center space-x-1">
            <span className="w-2 h-2 rounded-full bg-emerald-400 animate-pulse"></span>
            <span>Live Interceptor</span>
          </span>
        </div>

        <div className="overflow-x-auto max-h-60">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-800/60 text-slate-400 uppercase tracking-wider font-semibold sticky top-0 border-b border-slate-800">
              <tr>
                <th className="py-2 px-3">Timestamp</th>
                <th className="py-2 px-3">Consumer</th>
                <th className="py-2 px-3">Method</th>
                <th className="py-2 px-3">Endpoint</th>
                <th className="py-2 px-3">Status</th>
                <th className="py-2 px-3 text-right">Latency</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
              {logs.slice(0, 20).map(log => (
                <tr key={log.id} className="hover:bg-slate-800/30">
                  <td className="py-2 px-3 text-slate-400 text-[11px]">{new Date(log.timestamp).toLocaleTimeString()}</td>
                  <td className="py-2 px-3 font-sans text-slate-200">{log.consumerName}</td>
                  <td className="py-2 px-3 text-blue-400 font-bold">{log.httpMethod}</td>
                  <td className="py-2 px-3 text-slate-300">{log.endpoint}</td>
                  <td className="py-2 px-3">
                    <span className={`px-1.5 py-0.5 rounded text-[10px] ${
                      log.statusCode < 300 ? 'bg-emerald-500/20 text-emerald-400' : 'bg-rose-500/20 text-rose-400'
                    }`}>
                      {log.statusCode}
                    </span>
                  </td>
                  <td className="py-2 px-3 text-right font-bold text-slate-400">{log.responseTimeMs}ms</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Modal: Create API Key */}
      {showCreateModal && (
        <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4">
          <div className="bg-slate-900 border border-slate-800 rounded-xl max-w-md w-full p-6 shadow-2xl">
            <h3 className="text-base font-bold text-slate-100 mb-4">Generate Consumer App Token</h3>
            <form onSubmit={handleCreateKey} className="space-y-4">
              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Consumer App Name</label>
                <input
                  type="text"
                  value={consumerName}
                  onChange={e => setConsumerName(e.target.value)}
                  placeholder="e.g. itmCCbot, MarketInsights"
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none"
                  required
                />
              </div>

              <div>
                <label className="block text-xs font-medium text-slate-400 mb-1">Rate Limit (Req/Min)</label>
                <input
                  type="number"
                  value={rateLimit}
                  onChange={e => setRateLimit(parseInt(e.target.value))}
                  className="w-full px-3 py-2 bg-slate-800 border border-slate-700 rounded-lg text-sm text-white focus:outline-none"
                  required
                />
              </div>

              <div className="flex justify-end space-x-2 pt-3">
                <button
                  type="button"
                  onClick={() => setShowCreateModal(false)}
                  className="px-4 py-2 rounded-lg bg-slate-800 hover:bg-slate-700 text-slate-300 text-xs font-medium"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 rounded-lg bg-blue-600 hover:bg-blue-500 text-white text-xs font-medium"
                >
                  Generate Key
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

    </div>
  );
};
