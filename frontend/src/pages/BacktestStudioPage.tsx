import React, { useState, useEffect } from 'react';
import { Play, TrendingUp, DollarSign, Percent, Shield, ArrowUpRight, ArrowDownRight, Layers, Sliders, CheckCircle } from 'lucide-react';
import { MarketApi } from '../services/api';
import { BacktestRequest, BacktestResult, WatchlistSymbol } from '../types';
import { EquityCurveChart } from '../components/EquityCurveChart';

export const BacktestStudioPage: React.FC = () => {
  const [symbols, setSymbols] = useState<WatchlistSymbol[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<BacktestResult | null>(null);

  // Strategy Parameters
  const [symbol, setSymbol] = useState('AAPL');
  const [startDate, setStartDate] = useState('2025-01-01');
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [initialCapital, setInitialCapital] = useState(50000);
  const [targetDelta, setTargetDelta] = useState(0.70);
  const [targetDte, setTargetDte] = useState(30);
  const [minDte, setMinDte] = useState(14);
  const [maxDte, setMaxDte] = useState(45);
  const [profitTargetPct, setProfitTargetPct] = useState(65);
  const [rollOnDeltaBreach, setRollOnDeltaBreach] = useState(true);
  const [rollDeltaThreshold, setRollDeltaThreshold] = useState(0.50);
  const [slippage, setSlippage] = useState(0.02);
  const [commission, setCommission] = useState(0.65);

  useEffect(() => {
    const fetchSymbols = async () => {
      try {
        const list = await MarketApi.getWatchlist();
        setSymbols(list);
        if (list.length > 0) {
          setSymbol(list[0].symbol);
          if (list[0].earliestAvailableDate) setStartDate(list[0].earliestAvailableDate);
          if (list[0].latestAvailableDate) setEndDate(list[0].latestAvailableDate);
        }
      } catch (err) {
        console.error('Failed to load symbols:', err);
      }
    };
    fetchSymbols();
  }, []);

  const handleRunBacktest = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      setLoading(true);
      const req: BacktestRequest = {
        strategy: 'ITM_COVERED_CALL',
        symbol,
        startDate,
        endDate,
        initialCapital,
        targetDelta,
        deltaTolerance: 0.08,
        targetDte,
        minDte,
        maxDte,
        profitTargetPercent: profitTargetPct / 100,
        rollOnDeltaBreach,
        rollDeltaThreshold,
        closeDteThreshold: 2,
        slippagePerContract: slippage,
        commissionPerContract: commission
      };

      const res = await MarketApi.executeBacktest(req);
      setResult(res);
    } catch (err) {
      console.error('Backtest execution failed:', err);
    } finally {
      setLoading(false);
    }
  };

  // Run initial backtest on load once symbols ready
  useEffect(() => {
    if (symbol && startDate && endDate && !result) {
      handleRunBacktest({ preventDefault: () => {} } as any);
    }
  }, [symbol]);

  const metrics = result?.metrics;

  return (
    <div className="space-y-6">
      
      {/* Top Banner */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex items-center justify-between">
        <div>
          <h2 className="text-base font-bold text-slate-100 flex items-center space-x-2">
            <Play className="w-5 h-5 text-emerald-400" />
            <span>ITM Covered Call Strategy Backtest Studio</span>
          </h2>
          <p className="text-xs text-slate-400">
            Simulate and mark-to-market In-The-Money covered calls with dynamic rolling, profit-taking, and assignment logic
          </p>
        </div>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        
        {/* Strategy Parameters Form (1 Column) */}
        <div className="lg:col-span-1 bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
          <div className="flex items-center space-x-2 text-xs font-bold text-slate-200 uppercase tracking-wider border-b border-slate-800 pb-3">
            <Sliders className="w-4 h-4 text-blue-400" />
            <span>Strategy Parameters</span>
          </div>

          <form onSubmit={handleRunBacktest} className="space-y-3.5 text-xs">
            <div>
              <label className="block text-slate-400 mb-1">Underlying Ticker</label>
              <select
                value={symbol}
                onChange={e => setSymbol(e.target.value)}
                className="w-full px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono font-bold"
              >
                {symbols.map(s => (
                  <option key={s.id} value={s.symbol}>{s.symbol}</option>
                ))}
              </select>
            </div>

            <div className="grid grid-cols-2 gap-2">
              <div>
                <label className="block text-slate-400 mb-1">Start Date</label>
                <input
                  type="date"
                  value={startDate}
                  onChange={e => setStartDate(e.target.value)}
                  className="w-full px-2 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono text-[11px]"
                  required
                />
              </div>
              <div>
                <label className="block text-slate-400 mb-1">End Date</label>
                <input
                  type="date"
                  value={endDate}
                  onChange={e => setEndDate(e.target.value)}
                  className="w-full px-2 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono text-[11px]"
                  required
                />
              </div>
            </div>

            <div>
              <label className="block text-slate-400 mb-1">Initial Capital ($)</label>
              <input
                type="number"
                value={initialCapital}
                onChange={e => setInitialCapital(parseFloat(e.target.value))}
                className="w-full px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono"
                required
              />
            </div>

            <div>
              <div className="flex justify-between text-slate-400 mb-1">
                <span>Target Call Delta</span>
                <span className="text-blue-400 font-mono font-semibold">{targetDelta.toFixed(2)} Δ (ITM)</span>
              </div>
              <input
                type="range"
                min="0.50"
                max="0.90"
                step="0.05"
                value={targetDelta}
                onChange={e => setTargetDelta(parseFloat(e.target.value))}
                className="w-full accent-blue-500"
              />
            </div>

            <div className="grid grid-cols-3 gap-2">
              <div>
                <label className="block text-slate-400 mb-1">Target DTE</label>
                <input
                  type="number"
                  value={targetDte}
                  onChange={e => setTargetDte(parseInt(e.target.value))}
                  className="w-full px-2 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono"
                />
              </div>
              <div>
                <label className="block text-slate-400 mb-1">Min DTE</label>
                <input
                  type="number"
                  value={minDte}
                  onChange={e => setMinDte(parseInt(e.target.value))}
                  className="w-full px-2 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono"
                />
              </div>
              <div>
                <label className="block text-slate-400 mb-1">Max DTE</label>
                <input
                  type="number"
                  value={maxDte}
                  onChange={e => setMaxDte(parseInt(e.target.value))}
                  className="w-full px-2 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-white font-mono"
                />
              </div>
            </div>

            <div>
              <div className="flex justify-between text-slate-400 mb-1">
                <span>Profit Target (% of Max)</span>
                <span className="text-emerald-400 font-mono font-semibold">{profitTargetPct}%</span>
              </div>
              <input
                type="range"
                min="30"
                max="90"
                step="5"
                value={profitTargetPct}
                onChange={e => setProfitTargetPct(parseInt(e.target.value))}
                className="w-full accent-emerald-500"
              />
            </div>

            <div className="pt-1">
              <label className="flex items-center space-x-2 text-slate-300 cursor-pointer">
                <input
                  type="checkbox"
                  checked={rollOnDeltaBreach}
                  onChange={e => setRollOnDeltaBreach(e.target.checked)}
                  className="rounded accent-blue-600"
                />
                <span>Roll on Delta Breach (&lt; 0.50 Δ)</span>
              </label>
            </div>

            <div className="grid grid-cols-2 gap-2 pt-2 border-t border-slate-800">
              <div>
                <label className="block text-slate-400 mb-1">Slippage / Contract</label>
                <input
                  type="number"
                  step="0.01"
                  value={slippage}
                  onChange={e => setSlippage(parseFloat(e.target.value))}
                  className="w-full px-2 py-1 bg-slate-800 border border-slate-700 rounded text-white font-mono text-[11px]"
                />
              </div>
              <div>
                <label className="block text-slate-400 mb-1">Commission / Leg</label>
                <input
                  type="number"
                  step="0.05"
                  value={commission}
                  onChange={e => setCommission(parseFloat(e.target.value))}
                  className="w-full px-2 py-1 bg-slate-800 border border-slate-700 rounded text-white font-mono text-[11px]"
                />
              </div>
            </div>

            <button
              type="submit"
              disabled={loading}
              className="w-full mt-4 py-2.5 rounded-lg bg-emerald-600 hover:bg-emerald-500 text-white font-semibold text-xs shadow-lg shadow-emerald-600/30 transition-all flex items-center justify-center space-x-2 disabled:opacity-50"
            >
              <Play className={`w-4 h-4 fill-white ${loading ? 'animate-spin' : ''}`} />
              <span>{loading ? 'Executing Simulation...' : 'Execute Backtest'}</span>
            </button>
          </form>
        </div>

        {/* Results & Visuals (3 Columns) */}
        <div className="lg:col-span-3 space-y-6">
          
          {/* KPI Cards Grid */}
          {metrics && (
            <div className="grid grid-cols-2 sm:grid-cols-4 gap-3">
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-3.5 shadow-lg">
                <span className="text-[11px] text-slate-400">Total Net Return</span>
                <p className={`text-xl font-bold font-mono mt-1 ${metrics.totalReturnPercent >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                  {metrics.totalReturnPercent >= 0 ? '+' : ''}{metrics.totalReturnPercent.toFixed(2)}%
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5">${metrics.totalNetProfit.toLocaleString()}</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-3.5 shadow-lg">
                <span className="text-[11px] text-slate-400">Annualized CAGR</span>
                <p className="text-xl font-bold font-mono text-emerald-400 mt-1">
                  {metrics.cagrPercent.toFixed(2)}%
                </p>
                <p className="text-[11px] text-blue-400 mt-0.5">Alpha: +{metrics.alphaPercent.toFixed(2)}%</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-3.5 shadow-lg">
                <span className="text-[11px] text-slate-400">Sharpe / Sortino</span>
                <p className="text-xl font-bold font-mono text-slate-100 mt-1">
                  {metrics.sharpeRatio.toFixed(2)} <span className="text-xs text-slate-500">/ {metrics.sortinoRatio.toFixed(2)}</span>
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5">Vol: {metrics.annualizedVolatility.toFixed(1)}%</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-3.5 shadow-lg">
                <span className="text-[11px] text-slate-400">Win Rate / Drawdown</span>
                <p className="text-xl font-bold font-mono text-slate-100 mt-1">
                  {metrics.winRatePercent.toFixed(1)}%
                </p>
                <p className="text-[11px] text-rose-400 mt-0.5">Max DD: -{metrics.maxDrawdownPercent.toFixed(2)}%</p>
              </div>
            </div>
          )}

          {/* Equity Curve Chart */}
          {result && result.dailyEquityCurve.length > 0 && (
            <EquityCurveChart data={result.dailyEquityCurve} />
          )}

          {/* Trade Log Table */}
          {result && (
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
              <div className="flex items-center justify-between mb-4">
                <h3 className="text-sm font-bold text-slate-100">
                  Executed Trades Breakdown ({result.trades.length} cycles)
                </h3>
                <span className="text-xs text-slate-400 font-mono">
                  Profit Factor: <strong className="text-emerald-400">{metrics?.profitFactor}</strong>
                </span>
              </div>

              <div className="overflow-x-auto max-h-72">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-800/70 text-slate-400 uppercase tracking-wider font-semibold sticky top-0 border-b border-slate-800">
                    <tr>
                      <th className="py-2 px-3">#</th>
                      <th className="py-2 px-3">Dates</th>
                      <th className="py-2 px-3">Strike</th>
                      <th className="py-2 px-3">Entry/Exit Spot</th>
                      <th className="py-2 px-3">Premium In/Out</th>
                      <th className="py-2 px-3">Net P&L ($)</th>
                      <th className="py-2 px-3">ROC %</th>
                      <th className="py-2 px-3">Exit Reason</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
                    {result.trades.map(trade => (
                      <tr key={trade.id} className="hover:bg-slate-800/30">
                        <td className="py-2 px-3 font-semibold text-slate-400">{trade.tradeNumber}</td>
                        <td className="py-2 px-3 text-[11px]">
                          {trade.entryDate} → {trade.exitDate} ({trade.holdDays}d)
                        </td>
                        <td className="py-2 px-3 font-bold text-blue-400">${trade.strike.toFixed(2)}</td>
                        <td className="py-2 px-3 text-slate-400">
                          ${trade.stockEntryPrice.toFixed(2)} → ${trade.stockExitPrice.toFixed(2)}
                        </td>
                        <td className="py-2 px-3 text-slate-400">
                          ${trade.optionEntryPremium.toFixed(2)} → ${trade.optionExitPremium.toFixed(2)}
                        </td>
                        <td className={`py-2 px-3 font-bold ${trade.realizedPnlDollars >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                          {trade.realizedPnlDollars >= 0 ? '+' : ''}${trade.realizedPnlDollars.toFixed(2)}
                        </td>
                        <td className={`py-2 px-3 ${trade.returnOnCapitalPercent >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                          {trade.returnOnCapitalPercent >= 0 ? '+' : ''}{trade.returnOnCapitalPercent.toFixed(2)}%
                        </td>
                        <td className="py-2 px-3 font-sans">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                            trade.exitReason === 'ProfitTargetHit' ? 'bg-emerald-500/20 text-emerald-300' :
                            trade.exitReason === 'Assignment' ? 'bg-blue-500/20 text-blue-300' :
                            trade.exitReason === 'DeltaBreachRoll' ? 'bg-amber-500/20 text-amber-300' :
                            'bg-slate-800 text-slate-400'
                          }`}>
                            {trade.exitReason}
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          )}

        </div>

      </div>

    </div>
  );
};
