import React, { useState } from 'react';
import { X, HelpCircle, ArrowRight, Check, DollarSign, Layers, TrendingUp, ShieldCheck, Zap, Calculator } from 'lucide-react';
import { PositionSizingMode } from '../types';

interface Props {
  isOpen: boolean;
  onClose: () => void;
  currentMode: PositionSizingMode;
  fixedContracts: number;
  fixedDollarBudget: number;
  allocationPercent: number;
  onApply: (mode: PositionSizingMode, contracts: number, dollarBudget: number, allocPct: number) => void;
}

export const PositionSizingWizardModal: React.FC<Props> = ({
  isOpen,
  onClose,
  currentMode,
  fixedContracts: initialContracts,
  fixedDollarBudget: initialDollarBudget,
  allocationPercent: initialAllocPct,
  onApply
}) => {
  const [selectedMode, setSelectedMode] = useState<PositionSizingMode>(currentMode);
  const [contracts, setContracts] = useState(initialContracts || 1);
  const [dollarBudget, setDollarBudget] = useState(initialDollarBudget || 2500);
  const [allocPercent, setAllocPercent] = useState((initialAllocPct ? initialAllocPct * 100 : 10));

  if (!isOpen) return null;

  // Real UMAC Example Trade:
  // Stock Spot: $9.62, Sold $1.00 Call: $9.20 -> Net Debit: $0.42/share = $42/contract
  // Assignment at $1.00 -> $100 proceeds -> Net Profit per contract: $58.00 (+138.1% ROC)
  const exampleSpot = 9.62;
  const exampleCall = 9.20;
  const exampleDebitPerContract = (exampleSpot - exampleCall) * 100; // $42.00
  const exampleProfitPerContract = 58.00;
  const exampleRoc = 138.1;

  // Calculate live preview metrics based on mode
  let previewContracts = 1;
  if (selectedMode === 'FixedContracts') {
    previewContracts = Math.max(1, contracts);
  } else if (selectedMode === 'FixedDollarBudget') {
    previewContracts = Math.max(1, Math.floor(dollarBudget / exampleDebitPerContract));
  } else if (selectedMode === 'PortfolioCompoundingPercent') {
    // Assuming $50,000 portfolio
    const simulatedAccount = 50000;
    const allocatedCash = simulatedAccount * (allocPercent / 100);
    previewContracts = Math.max(1, Math.floor(allocatedCash / exampleDebitPerContract));
  }

  const previewOutlay = previewContracts * exampleDebitPerContract;
  const previewTotalProfit = previewContracts * exampleProfitPerContract;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-md animate-fadeIn">
      <div className="relative w-full max-w-4xl max-h-[90vh] overflow-y-auto bg-slate-900 border border-slate-700/80 rounded-2xl shadow-2xl p-6 text-slate-200">
        
        {/* Header */}
        <div className="flex items-center justify-between pb-4 border-b border-slate-800">
          <div className="flex items-center space-x-3">
            <div className="p-2.5 bg-blue-500/10 border border-blue-500/30 rounded-xl text-blue-400">
              <Calculator className="w-6 h-6" />
            </div>
            <div>
              <h3 className="text-lg font-bold text-white flex items-center space-x-2">
                <span>Position Sizing & Capital Allocation Wizard</span>
                <span className="text-[10px] uppercase font-bold tracking-wider px-2 py-0.5 bg-emerald-500/20 text-emerald-400 rounded-full border border-emerald-500/30">
                  Transparency Engine
                </span>
              </h3>
              <p className="text-xs text-slate-400">
                Understand how deep In-The-Money covered calls scale capital and choose your preferred backtest sizing model
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="p-1.5 rounded-lg text-slate-400 hover:text-white hover:bg-slate-800 transition"
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Deep ITM Leverage Educational Alert */}
        <div className="mt-5 p-4 bg-gradient-to-r from-blue-950/40 via-indigo-950/30 to-purple-950/40 border border-blue-800/40 rounded-xl">
          <div className="flex items-start space-x-3">
            <Zap className="w-5 h-5 text-amber-400 flex-shrink-0 mt-0.5" />
            <div className="text-xs space-y-1">
              <span className="font-bold text-slate-100">Why do Deep ITM Covered Calls produce massive % returns?</span>
              <p className="text-slate-300 leading-relaxed">
                When buying stock at <strong className="text-white">$9.62</strong> and selling a deep <strong className="text-white">$1.00 Call</strong> for <strong className="text-white">$9.20</strong>, your required cash outlay is only <strong className="text-emerald-400">$0.42 per share ($42 / contract)</strong>.
                When assigned at the $1.00 strike, you take in <strong className="text-white">$100</strong>, earning <strong className="text-emerald-400">+$58.00 per contract (+138.1% ROC)</strong> on just a $42 investment!
              </p>
            </div>
          </div>
        </div>

        {/* 3 Sizing Cards */}
        <div className="mt-6 grid grid-cols-1 md:grid-cols-3 gap-4">
          
          {/* Mode 1: Fixed Single Contract */}
          <div
            onClick={() => setSelectedMode('FixedContracts')}
            className={`cursor-pointer rounded-xl p-4 border transition-all relative flex flex-col justify-between ${
              selectedMode === 'FixedContracts'
                ? 'bg-blue-950/40 border-blue-500 ring-2 ring-blue-500/30 shadow-lg shadow-blue-950/50'
                : 'bg-slate-800/50 border-slate-700 hover:border-slate-600 hover:bg-slate-800'
            }`}
          >
            <div>
              <div className="flex items-center justify-between">
                <span className="p-2 bg-blue-500/20 text-blue-400 rounded-lg">
                  <Layers className="w-4 h-4" />
                </span>
                {selectedMode === 'FixedContracts' && (
                  <span className="flex items-center text-[10px] font-bold text-blue-400 bg-blue-500/20 px-2 py-0.5 rounded-full">
                    <Check className="w-3 h-3 mr-1" /> Active
                  </span>
                )}
              </div>
              <h4 className="font-bold text-white text-sm mt-3">1. Fixed Contract Baseline</h4>
              <p className="text-[11px] text-slate-400 mt-1">
                Trades a fixed number of contracts per trade setup. Ideal for pure per-contract strategy analysis.
              </p>
            </div>

            {selectedMode === 'FixedContracts' && (
              <div className="mt-4 pt-3 border-t border-blue-800/40">
                <label className="block text-[10px] uppercase font-bold text-blue-300 mb-1">Contract Quantity</label>
                <div className="flex items-center space-x-2">
                  <input
                    type="number"
                    min="1"
                    max="100"
                    value={contracts}
                    onChange={e => setContracts(parseInt(e.target.value) || 1)}
                    className="w-full px-2.5 py-1.5 bg-slate-900 border border-blue-500/50 rounded-lg text-xs font-mono font-bold text-white focus:outline-none"
                  />
                  <span className="text-xs text-slate-400">Contracts</span>
                </div>
              </div>
            )}
          </div>

          {/* Mode 2: Fixed Dollar Budget */}
          <div
            onClick={() => setSelectedMode('FixedDollarBudget')}
            className={`cursor-pointer rounded-xl p-4 border transition-all relative flex flex-col justify-between ${
              selectedMode === 'FixedDollarBudget'
                ? 'bg-purple-950/40 border-purple-500 ring-2 ring-purple-500/30 shadow-lg shadow-purple-950/50'
                : 'bg-slate-800/50 border-slate-700 hover:border-slate-600 hover:bg-slate-800'
            }`}
          >
            <div>
              <div className="flex items-center justify-between">
                <span className="p-2 bg-purple-500/20 text-purple-400 rounded-lg">
                  <DollarSign className="w-4 h-4" />
                </span>
                {selectedMode === 'FixedDollarBudget' && (
                  <span className="flex items-center text-[10px] font-bold text-purple-400 bg-purple-500/20 px-2 py-0.5 rounded-full">
                    <Check className="w-3 h-3 mr-1" /> Active
                  </span>
                )}
              </div>
              <h4 className="font-bold text-white text-sm mt-3">2. Fixed Dollar Budget Cap</h4>
              <p className="text-[11px] text-slate-400 mt-1">
                Allocates a fixed dollar amount per trade setup. Sizes contract count to fit within your maximum risk cap.
              </p>
            </div>

            {selectedMode === 'FixedDollarBudget' && (
              <div className="mt-4 pt-3 border-t border-purple-800/40">
                <label className="block text-[10px] uppercase font-bold text-purple-300 mb-1">Max Risk Budget ($)</label>
                <div className="flex items-center space-x-2">
                  <span className="text-xs text-slate-400 font-bold">$</span>
                  <input
                    type="number"
                    step="100"
                    min="500"
                    max="100000"
                    value={dollarBudget}
                    onChange={e => setDollarBudget(parseFloat(e.target.value) || 2500)}
                    className="w-full px-2.5 py-1.5 bg-slate-900 border border-purple-500/50 rounded-lg text-xs font-mono font-bold text-white focus:outline-none"
                  />
                </div>
              </div>
            )}
          </div>

          {/* Mode 3: Dynamic Portfolio Compounding */}
          <div
            onClick={() => setSelectedMode('PortfolioCompoundingPercent')}
            className={`cursor-pointer rounded-xl p-4 border transition-all relative flex flex-col justify-between ${
              selectedMode === 'PortfolioCompoundingPercent'
                ? 'bg-emerald-950/40 border-emerald-500 ring-2 ring-emerald-500/30 shadow-lg shadow-emerald-950/50'
                : 'bg-slate-800/50 border-slate-700 hover:border-slate-600 hover:bg-slate-800'
            }`}
          >
            <div>
              <div className="flex items-center justify-between">
                <span className="p-2 bg-emerald-500/20 text-emerald-400 rounded-lg">
                  <TrendingUp className="w-4 h-4" />
                </span>
                {selectedMode === 'PortfolioCompoundingPercent' && (
                  <span className="flex items-center text-[10px] font-bold text-emerald-400 bg-emerald-500/20 px-2 py-0.5 rounded-full">
                    <Check className="w-3 h-3 mr-1" /> Active
                  </span>
                )}
              </div>
              <h4 className="font-bold text-white text-sm mt-3">3. Portfolio Compounding</h4>
              <p className="text-[11px] text-slate-400 mt-1">
                Reinvests winnings by allocating a percentage of the total growing account cash to each cycle.
              </p>
            </div>

            {selectedMode === 'PortfolioCompoundingPercent' && (
              <div className="mt-4 pt-3 border-t border-emerald-800/40">
                <div className="flex justify-between items-center mb-1">
                  <label className="text-[10px] uppercase font-bold text-emerald-300">Cash Allocation</label>
                  <span className="text-xs font-mono font-bold text-emerald-400">{allocPercent}%</span>
                </div>
                <input
                  type="range"
                  min="1"
                  max="100"
                  value={allocPercent}
                  onChange={e => setAllocPercent(parseInt(e.target.value))}
                  className="w-full accent-emerald-500 cursor-pointer"
                />
              </div>
            )}
          </div>

        </div>

        {/* Live Simulation Preview with UMAC Trade */}
        <div className="mt-6 bg-slate-950/60 border border-slate-800 rounded-xl p-4">
          <div className="flex items-center justify-between mb-3 pb-2 border-b border-slate-800/60">
            <span className="text-xs font-bold text-slate-300 flex items-center space-x-2">
              <span>Real Trade Simulation Preview (UMAC Aug 20, 2025 Trade #1)</span>
            </span>
            <span className="text-[11px] font-mono text-slate-400">
              Net Debit / Share: <strong className="text-white">$0.42 ($42/contract)</strong>
            </span>
          </div>

          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-center">
            <div className="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800">
              <span className="text-[10px] uppercase font-semibold text-slate-500">Contracts Sized</span>
              <p className="text-base font-bold font-mono text-blue-400 mt-0.5">{previewContracts}x ({previewContracts * 100} shs)</p>
            </div>
            <div className="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800">
              <span className="text-[10px] uppercase font-semibold text-slate-500">Capital Outlay</span>
              <p className="text-base font-bold font-mono text-amber-400 mt-0.5">${previewOutlay.toFixed(2)}</p>
            </div>
            <div className="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800">
              <span className="text-[10px] uppercase font-semibold text-slate-500">Total Net P&L</span>
              <p className="text-base font-bold font-mono text-emerald-400 mt-0.5">+${previewTotalProfit.toFixed(2)}</p>
            </div>
            <div className="bg-slate-900/80 p-2.5 rounded-lg border border-slate-800">
              <span className="text-[10px] uppercase font-semibold text-slate-500">Realized ROC</span>
              <p className="text-base font-bold font-mono text-emerald-400 mt-0.5">+{exampleRoc.toFixed(1)}%</p>
            </div>
          </div>

          <div className="mt-3 text-[11px] text-slate-400 flex items-center justify-between">
            <span>
              Formula: <code className="text-slate-300 bg-slate-900 px-1.5 py-0.5 rounded font-mono">{previewContracts} contracts &times; $58.00 profit = ${previewTotalProfit.toFixed(2)} Total Net P&L</code>
            </span>
          </div>
        </div>

        {/* Footer Actions */}
        <div className="mt-6 pt-4 border-t border-slate-800 flex items-center justify-between">
          <button
            onClick={onClose}
            className="px-4 py-2 text-xs font-semibold text-slate-400 hover:text-white transition"
          >
            Cancel
          </button>
          
          <button
            onClick={() => {
              onApply(selectedMode, contracts, dollarBudget, allocPercent / 100);
              onClose();
            }}
            className="flex items-center space-x-2 px-5 py-2.5 bg-gradient-to-r from-blue-600 to-indigo-600 hover:from-blue-500 hover:to-indigo-500 text-white text-xs font-bold rounded-xl shadow-lg shadow-blue-600/30 transition transform hover:-translate-y-0.5"
          >
            <span>Apply Sizing & Run Backtest</span>
            <ArrowRight className="w-4 h-4" />
          </button>
        </div>

      </div>
    </div>
  );
};
