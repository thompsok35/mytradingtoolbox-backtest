import React, { useState, useEffect } from 'react';
import { Play, TrendingUp, DollarSign, Percent, Shield, ArrowUpRight, ArrowDownRight, Layers, Sliders, CheckCircle, Calculator, Sparkles, HelpCircle } from 'lucide-react';
import { MarketApi } from '../services/api';
import { BacktestRequest, BacktestResult, WatchlistSymbol, PositionSizingMode } from '../types';
import { EquityCurveChart } from '../components/EquityCurveChart';
import { PositionSizingWizardModal } from '../components/PositionSizingWizardModal';

export const BacktestStudioPage: React.FC = () => {
  const [symbols, setSymbols] = useState<WatchlistSymbol[]>([]);
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<BacktestResult | null>(null);
  const [isWizardOpen, setIsWizardOpen] = useState(false);

  // Strategy Parameters
  const [symbol, setSymbol] = useState('AAPL');
  const [startDate, setStartDate] = useState('2025-01-01');
  const [endDate, setEndDate] = useState(new Date().toISOString().split('T')[0]);
  const [initialCapital, setInitialCapital] = useState(50000);
  
  // Position Sizing Methodology
  const [sizingMode, setSizingMode] = useState<PositionSizingMode>('FixedContracts');
  const [fixedContracts, setFixedContracts] = useState(1);
  const [fixedDollarBudget, setFixedDollarBudget] = useState(2500);
  const [allocationPercent, setAllocationPercent] = useState(0.10);

  // Covered Call Strike & DTE Rules
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

  const handleRunBacktest = async (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    try {
      setLoading(true);
      const req: BacktestRequest = {
        strategy: 'ITM_COVERED_CALL',
        symbol,
        startDate,
        endDate,
        initialCapital,
        sizingMode,
        fixedContracts,
        fixedDollarBudget,
        allocationPercent,
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
      handleRunBacktest();
    }
  }, [symbol]);

  const handleApplyWizard = (mode: PositionSizingMode, contractsCount: number, dollarCap: number, allocPct: number) => {
    setSizingMode(mode);
    setFixedContracts(contractsCount);
    setFixedDollarBudget(dollarCap);
    setAllocationPercent(allocPct);

    // Run backtest with applied wizard parameters
    setTimeout(() => {
      handleRunBacktest();
    }, 50);
  };

  const metrics = result?.metrics;

  return (
    <div className="space-y-6">
      
      {/* Top Banner with Wizard Trigger */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h2 className="text-base font-bold text-slate-100 flex items-center space-x-2">
            <Play className="w-5 h-5 text-emerald-400" />
            <span>ITM Covered Call Strategy Backtest Studio</span>
          </h2>
          <p className="text-xs text-slate-400 mt-0.5">
            Simulate and mark-to-market In-The-Money covered calls with dynamic rolling, profit-taking, and assignment logic
          </p>
        </div>
        
        <button
          onClick={() => setIsWizardOpen(true)}
          className="flex items-center space-x-2 px-3.5 py-2 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white rounded-lg text-xs font-bold shadow-md shadow-blue-500/20 transition whitespace-nowrap self-start sm:self-auto"
        >
          <Sparkles className="w-4 h-4 text-amber-300" />
          <span>Position Sizing Wizard</span>
        </button>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-4 gap-6">
        
        {/* Strategy Parameters Form (1 Column) */}
        <div className="lg:col-span-1 bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg space-y-4">
          <div className="flex items-center justify-between border-b border-slate-800 pb-3">
            <div className="flex items-center space-x-2 text-xs font-bold text-slate-200 uppercase tracking-wider">
              <Sliders className="w-4 h-4 text-blue-400" />
              <span>Strategy Parameters</span>
            </div>
            <button
              type="button"
              onClick={() => setIsWizardOpen(true)}
              className="text-[11px] text-blue-400 hover:text-blue-300 font-semibold flex items-center space-x-1"
            >
              <Calculator className="w-3.5 h-3.5" />
              <span>Sizing Guide</span>
            </button>
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

            {/* Position Sizing Methodology Box */}
            <div className="p-3 bg-slate-950/60 border border-slate-800 rounded-xl space-y-2.5">
              <div className="flex items-center justify-between">
                <label className="block text-[11px] font-bold text-slate-300">Position Sizing Mode</label>
                <button
                  type="button"
                  onClick={() => setIsWizardOpen(true)}
                  className="text-[10px] text-blue-400 hover:underline flex items-center"
                >
                  <HelpCircle className="w-3 h-3 mr-0.5" /> Explain
                </button>
              </div>

              <div className="grid grid-cols-3 gap-1 p-1 bg-slate-900 rounded-lg border border-slate-800 text-[10px] font-semibold text-center">
                <button
                  type="button"
                  onClick={() => setSizingMode('FixedContracts')}
                  className={`py-1 rounded transition ${
                    sizingMode === 'FixedContracts' ? 'bg-blue-600 text-white font-bold' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  1x Fixed
                </button>
                <button
                  type="button"
                  onClick={() => setSizingMode('FixedDollarBudget')}
                  className={`py-1 rounded transition ${
                    sizingMode === 'FixedDollarBudget' ? 'bg-purple-600 text-white font-bold' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  $ Cap
                </button>
                <button
                  type="button"
                  onClick={() => setSizingMode('PortfolioCompoundingPercent')}
                  className={`py-1 rounded transition ${
                    sizingMode === 'PortfolioCompoundingPercent' ? 'bg-emerald-600 text-white font-bold' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  % Equity
                </button>
              </div>

              {sizingMode === 'FixedContracts' && (
                <div>
                  <div className="flex justify-between text-slate-400 mb-1 text-[11px]">
                    <span>Contract Size</span>
                    <span className="font-mono text-blue-400 font-bold">{fixedContracts} Contract(s)</span>
                  </div>
                  <input
                    type="number"
                    min="1"
                    max="100"
                    value={fixedContracts}
                    onChange={e => setFixedContracts(parseInt(e.target.value) || 1)}
                    className="w-full px-2 py-1 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono text-xs"
                  />
                </div>
              )}

              {sizingMode === 'FixedDollarBudget' && (
                <div>
                  <div className="flex justify-between text-slate-400 mb-1 text-[11px]">
                    <span>Risk Budget Cap</span>
                    <span className="font-mono text-purple-400 font-bold">${fixedDollarBudget}</span>
                  </div>
                  <input
                    type="number"
                    step="100"
                    min="500"
                    max="100000"
                    value={fixedDollarBudget}
                    onChange={e => setFixedDollarBudget(parseFloat(e.target.value) || 2500)}
                    className="w-full px-2 py-1 bg-slate-900 border border-slate-700 rounded-lg text-white font-mono text-xs"
                  />
                </div>
              )}

              {sizingMode === 'PortfolioCompoundingPercent' && (
                <div>
                  <div className="flex justify-between text-slate-400 mb-1 text-[11px]">
                    <span>Cash Allocation</span>
                    <span className="font-mono text-emerald-400 font-bold">{(allocationPercent * 100).toFixed(0)}%</span>
                  </div>
                  <input
                    type="range"
                    min="1"
                    max="100"
                    value={allocationPercent * 100}
                    onChange={e => setAllocationPercent(parseInt(e.target.value) / 100)}
                    className="w-full accent-emerald-500"
                  />
                </div>
              )}
            </div>

            <div>
              <label className="block text-slate-400 mb-1">Simulated Starting Capital ($)</label>
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
                <span>Profit Target (%)</span>
                <span className="text-emerald-400 font-mono font-semibold">{profitTargetPct}%</span>
              </div>
              <input
                type="range"
                min="30"
                max="100"
                step="5"
                value={profitTargetPct}
                onChange={e => setProfitTargetPct(parseInt(e.target.value))}
                className="w-full accent-emerald-500"
              />
            </div>

            <div className="pt-2">
              <button
                type="submit"
                disabled={loading}
                className="w-full py-2.5 bg-emerald-600 hover:bg-emerald-500 text-white font-bold rounded-lg transition shadow-lg shadow-emerald-950 flex items-center justify-center space-x-2"
              >
                <Play className="w-4 h-4 fill-current" />
                <span>{loading ? 'Simulating Historical Cycles...' : 'Run 365-Day Backtest'}</span>
              </button>
            </div>
          </form>
        </div>

        {/* Results & Visual Analytics (3 Columns) */}
        <div className="lg:col-span-3 space-y-6">
          
          {/* Key Metric Scorecard */}
          {metrics && (
            <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
                <span className="text-xs text-slate-400 font-medium">Total Return</span>
                <p className={`text-xl font-bold font-mono mt-1 ${metrics.totalReturnPercent >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                  {metrics.totalReturnPercent >= 0 ? '+' : ''}{metrics.totalReturnPercent.toFixed(2)}%
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5">${metrics.totalNetProfit.toLocaleString()} Net P&L</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
                <span className="text-xs text-slate-400 font-medium">Win Rate</span>
                <p className="text-xl font-bold font-mono text-emerald-400 mt-1">
                  {metrics.winRatePercent.toFixed(1)}%
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5">{metrics.winningTrades}W / {metrics.losingTrades}L ({metrics.totalTrades} total)</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
                <span className="text-xs text-slate-400 font-medium">Avg Trade ROC</span>
                <p className="text-xl font-bold font-mono text-blue-400 mt-1">
                  +{metrics?.averageWinningTradePnl && metrics.averageWinningTradePnl > 0 ? ((metrics.averageTradePnl || 0) / Math.max(1, metrics.averageWinningTradePnl) * 100).toFixed(1) : '0'}%
                </p>
                <p className="text-[11px] text-slate-500 mt-0.5">Hold: {(metrics?.averageHoldDays || 0).toFixed(1)}d per cycle</p>
              </div>

              <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
                <span className="text-xs text-slate-400 font-medium">Sharpe Ratio</span>
                <p className="text-xl font-bold font-mono text-purple-400 mt-1">
                  {metrics.sharpeRatio.toFixed(2)}
                </p>
                <p className="text-[11px] text-rose-400 mt-0.5">Max DD: -{metrics.maxDrawdownPercent.toFixed(2)}%</p>
              </div>
            </div>
          )}

          {/* Equity Curve Chart */}
          {result && result.dailyEquityCurve.length > 0 && (
            <EquityCurveChart data={result.dailyEquityCurve} />
          )}

          {/* Trade Log Table with Contracts and Outlay Columns */}
          {result && (
            <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
              <div className="flex items-center justify-between mb-4">
                <div>
                  <h3 className="text-sm font-bold text-slate-100 flex items-center space-x-2">
                    <span>Executed Trades Breakdown ({result.trades.length} completed cycles)</span>
                    <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 bg-blue-500/20 text-blue-400 rounded-full border border-blue-500/30">
                      {sizingMode === 'FixedContracts' ? `${fixedContracts}x Contract Baseline` : sizingMode === 'FixedDollarBudget' ? `$${fixedDollarBudget} Budget Cap` : `${(allocationPercent * 100).toFixed(0)}% Compounding`}
                    </span>
                  </h3>
                </div>
                <span className="text-xs text-slate-400 font-mono">
                  Profit Factor: <strong className="text-emerald-400">{metrics?.profitFactor}</strong>
                </span>
              </div>

              <div className="overflow-x-auto max-h-80">
                <table className="w-full text-left text-xs">
                  <thead className="bg-slate-800/70 text-slate-400 uppercase tracking-wider font-semibold sticky top-0 border-b border-slate-800">
                    <tr>
                      <th className="py-2.5 px-3">#</th>
                      <th className="py-2.5 px-3">Dates</th>
                      <th className="py-2.5 px-3">Strike & Exp</th>
                      <th className="py-2.5 px-3">Contracts</th>
                      <th className="py-2.5 px-3">Debit / Sh</th>
                      <th className="py-2.5 px-3">Total Outlay</th>
                      <th className="py-2.5 px-3">Realized ROC</th>
                      <th className="py-2.5 px-3">Total Net P&L</th>
                      <th className="py-2.5 px-3">Exit Reason</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
                    {result.trades.map(trade => (
                      <tr key={trade.id} className="hover:bg-slate-800/40 transition">
                        <td className="py-2.5 px-3 font-semibold text-slate-400">{trade.tradeNumber}</td>
                        <td className="py-2.5 px-3 text-[11px]">
                          {trade.entryDate} → {trade.exitDate} <span className="text-slate-500">({trade.holdDays}d)</span>
                        </td>
                        <td className="py-2.5 px-3">
                          <span className="font-bold text-blue-400">${trade.strike.toFixed(2)} Call</span>
                        </td>
                        <td className="py-2.5 px-3">
                          <span className="font-bold text-purple-300 bg-purple-950/50 px-2 py-0.5 rounded border border-purple-800/50">
                            {trade.contracts}x
                          </span>
                        </td>
                        <td className="py-2.5 px-3 text-slate-300">
                          ${trade.netDebitPaid ? trade.netDebitPaid.toFixed(2) : ((trade.stockEntryPrice - trade.optionEntryPremium)).toFixed(2)}
                        </td>
                        <td className="py-2.5 px-3 text-amber-300 font-semibold">
                          ${trade.totalDebitOutlay ? trade.totalDebitOutlay.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : ((trade.netDebitPaid || (trade.stockEntryPrice - trade.optionEntryPremium)) * trade.contracts * 100).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
                        </td>
                        <td className={`py-2.5 px-3 font-bold ${trade.returnOnCapitalPercent && trade.returnOnCapitalPercent >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                          {trade.returnOnCapitalPercent && trade.returnOnCapitalPercent >= 0 ? '+' : ''}{trade.returnOnCapitalPercent ? trade.returnOnCapitalPercent.toFixed(1) : '0.0'}%
                        </td>
                        <td className={`py-2.5 px-3 font-bold ${trade.realizedPnlDollars && trade.realizedPnlDollars >= 0 ? 'text-emerald-400' : 'text-rose-400'}`}>
                          {trade.realizedPnlDollars && trade.realizedPnlDollars >= 0 ? '+' : ''}${trade.realizedPnlDollars ? trade.realizedPnlDollars.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }) : '0.00'}
                        </td>
                        <td className="py-2.5 px-3 font-sans">
                          <span className={`px-2 py-0.5 rounded text-[10px] font-semibold ${
                            trade.exitReason === 'ProfitTargetHit' ? 'bg-emerald-500/20 text-emerald-300 border border-emerald-500/30' :
                            trade.exitReason === 'Assignment' ? 'bg-blue-500/20 text-blue-300 border border-blue-500/30' :
                            trade.exitReason === 'DeltaBreachRoll' ? 'bg-amber-500/20 text-amber-300 border border-amber-500/30' :
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

      {/* Position Sizing Wizard Modal */}
      <PositionSizingWizardModal
        isOpen={isWizardOpen}
        onClose={() => setIsWizardOpen(false)}
        currentMode={sizingMode}
        fixedContracts={fixedContracts}
        fixedDollarBudget={fixedDollarBudget}
        allocationPercent={allocationPercent}
        onApply={handleApplyWizard}
      />

    </div>
  );
};
