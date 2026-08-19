import React, { useState, useEffect } from 'react';
import { Calendar, Filter, TrendingUp, BarChart3, LineChart, Layers, Search } from 'lucide-react';
import { MarketApi } from '../services/api';
import { OptionChainResponseDto, OptionContractDto, WatchlistSymbol } from '../types';
import { PayoffChart } from '../components/PayoffChart';
import { VolatilitySkewChart } from '../components/VolatilitySkewChart';

export const TimeTravelExplorerPage: React.FC = () => {
  const [symbols, setSymbols] = useState<WatchlistSymbol[]>([]);
  const [selectedSymbol, setSelectedSymbol] = useState('AAPL');
  const [selectedDate, setSelectedDate] = useState<string>('');
  const [selectedExpiration, setSelectedExpiration] = useState<string>('all');
  const [activeSideTab, setActiveSideTab] = useState<'calls' | 'puts' | 'both'>('both');
  const [viewMode, setViewMode] = useState<'table' | 'charts'>('table');

  const [chainData, setChainData] = useState<OptionChainResponseDto | null>(null);
  const [loading, setLoading] = useState(false);

  // Filters
  const [minDte, setMinDte] = useState<number | undefined>();
  const [maxDte, setMaxDte] = useState<number | undefined>();
  const [selectedStrikePayoff, setSelectedStrikePayoff] = useState<number | null>(null);
  const [selectedPremiumPayoff, setSelectedPremiumPayoff] = useState<number | null>(null);

  useEffect(() => {
    const fetchSymbols = async () => {
      try {
        const list = await MarketApi.getWatchlist();
        setSymbols(list);
        if (list.length > 0) {
          const first = list[0];
          setSelectedSymbol(first.symbol);
          if (first.latestAvailableDate) {
            setSelectedDate(first.latestAvailableDate);
          }
        }
      } catch (err) {
        console.error('Failed to load symbols:', err);
      }
    };
    fetchSymbols();
  }, []);

  const loadChain = async () => {
    if (!selectedSymbol) return;
    try {
      setLoading(true);
      const data = await MarketApi.getOptionChain({
        symbol: selectedSymbol,
        date: selectedDate || undefined,
        minDte,
        maxDte
      });
      setChainData(data);
      if (data.calls.length > 0) {
        setSelectedStrikePayoff(data.calls[0].strike);
        setSelectedPremiumPayoff(data.calls[0].bid);
      }
    } catch (err) {
      console.error('Failed to fetch chain:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (selectedSymbol) {
      loadChain();
    }
  }, [selectedSymbol, selectedDate, minDte, maxDte]);

  // Unique expiration dates
  const expirations = Array.from(
    new Set([
      ...(chainData?.calls.map(c => c.expirationDate) || []),
      ...(chainData?.puts.map(p => p.expirationDate) || [])
    ])
  ).sort();

  const filteredCalls = chainData?.calls.filter(c => selectedExpiration === 'all' || c.expirationDate === selectedExpiration) || [];
  const filteredPuts = chainData?.puts.filter(p => selectedExpiration === 'all' || p.expirationDate === selectedExpiration) || [];

  // Group by strike for dual view
  const strikes = Array.from(
    new Set([
      ...filteredCalls.map(c => c.strike),
      ...filteredPuts.map(p => p.strike)
    ])
  ).sort((a, b) => a - b);

  const callsByStrike = new Map(filteredCalls.map(c => [c.strike, c]));
  const putsByStrike = new Map(filteredPuts.map(p => [p.strike, p]));

  const spot = chainData?.underlyingPrice || 100;

  return (
    <div className="space-y-6">
      
      {/* Top Header & Search Bar */}
      <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
        <div className="flex flex-col md:flex-row md:items-center justify-between gap-4">
          <div>
            <h2 className="text-base font-bold text-slate-100 flex items-center space-x-2">
              <Calendar className="w-5 h-5 text-blue-400" />
              <span>Time-Travel Option Chain Explorer</span>
            </h2>
            <p className="text-xs text-slate-400">
              Reconstruct the full market state and option Greeks as they existed on any historical trading session
            </p>
          </div>

          <div className="flex flex-wrap items-center gap-3">
            {/* Symbol Picker */}
            <div>
              <label className="block text-[10px] uppercase font-bold text-slate-500 mb-1">Symbol</label>
              <select
                value={selectedSymbol}
                onChange={e => {
                  setSelectedSymbol(e.target.value);
                  const s = symbols.find(x => x.symbol === e.target.value);
                  if (s?.latestAvailableDate) setSelectedDate(s.latestAvailableDate);
                }}
                className="px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-xs font-mono font-bold text-blue-400 focus:outline-none"
              >
                {symbols.map(s => (
                  <option key={s.id} value={s.symbol}>{s.symbol} ({s.totalSnapshotDays} days)</option>
                ))}
              </select>
            </div>

            {/* Date Picker */}
            <div>
              <label className="block text-[10px] uppercase font-bold text-slate-500 mb-1">Historical Date</label>
              <input
                type="date"
                value={selectedDate}
                onChange={e => setSelectedDate(e.target.value)}
                className="px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-xs font-mono text-white focus:outline-none"
              />
            </div>

            {/* Expiration Filter */}
            <div>
              <label className="block text-[10px] uppercase font-bold text-slate-500 mb-1">Expiration</label>
              <select
                value={selectedExpiration}
                onChange={e => setSelectedExpiration(e.target.value)}
                className="px-3 py-1.5 bg-slate-800 border border-slate-700 rounded-lg text-xs text-white focus:outline-none"
              >
                <option value="all">All Expirations ({expirations.length})</option>
                {expirations.map(exp => (
                  <option key={exp} value={exp}>{exp}</option>
                ))}
              </select>
            </div>

            {/* View Mode Toggle */}
            <div>
              <label className="block text-[10px] uppercase font-bold text-slate-500 mb-1">Mode</label>
              <div className="flex bg-slate-800 p-0.5 rounded-lg border border-slate-700">
                <button
                  onClick={() => setViewMode('table')}
                  className={`px-3 py-1 text-xs rounded-md font-medium transition-all ${
                    viewMode === 'table' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  Table
                </button>
                <button
                  onClick={() => setViewMode('charts')}
                  className={`px-3 py-1 text-xs rounded-md font-medium transition-all ${
                    viewMode === 'charts' ? 'bg-blue-600 text-white' : 'text-slate-400 hover:text-white'
                  }`}
                >
                  Visuals
                </button>
              </div>
            </div>

          </div>
        </div>

        {/* Spot Price Bar */}
        {chainData && (
          <div className="mt-4 pt-4 border-t border-slate-800 flex flex-wrap items-center justify-between text-xs">
            <div className="flex items-center space-x-4">
              <div>
                <span className="text-slate-400">Snapshot Session: </span>
                <span className="font-mono font-semibold text-slate-200">{chainData.snapshotDate}</span>
              </div>
              <div>
                <span className="text-slate-400">Spot Price: </span>
                <span className="font-mono font-bold text-emerald-400 text-sm">${spot.toFixed(2)}</span>
              </div>
              <div>
                <span className="text-slate-400">Total Contracts: </span>
                <span className="font-mono text-slate-200">{chainData.calls.length + chainData.puts.length}</span>
              </div>
            </div>

            <div className="flex items-center space-x-2">
              <span className="text-slate-500 text-[11px]">DTE Filter:</span>
              <input
                type="number"
                placeholder="Min DTE"
                value={minDte || ''}
                onChange={e => setMinDte(e.target.value ? parseInt(e.target.value) : undefined)}
                className="w-16 px-2 py-0.5 bg-slate-800 border border-slate-700 rounded text-xs text-white"
              />
              <span className="text-slate-500">-</span>
              <input
                type="number"
                placeholder="Max DTE"
                value={maxDte || ''}
                onChange={e => setMaxDte(e.target.value ? parseInt(e.target.value) : undefined)}
                className="w-16 px-2 py-0.5 bg-slate-800 border border-slate-700 rounded text-xs text-white"
              />
            </div>
          </div>
        )}
      </div>

      {/* Visual Mode (Payoff & Skew) */}
      {viewMode === 'charts' && (
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
          <VolatilitySkewChart
            calls={filteredCalls}
            puts={filteredPuts}
            underlyingPrice={spot}
          />
          <PayoffChart
            spotPrice={spot}
            strikePrice={selectedStrikePayoff || spot * 0.95}
            premiumReceived={selectedPremiumPayoff || spot * 0.08}
          />
        </div>
      )}

      {/* Table Mode: Full Dual Option Chain */}
      {viewMode === 'table' && (
        <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
          <div className="flex items-center justify-between mb-4">
            <h3 className="text-sm font-bold text-slate-100">
              Reconstructed Option Chain ({selectedSymbol} - {selectedDate})
            </h3>
            <div className="flex space-x-1 bg-slate-800 p-0.5 rounded-lg text-xs">
              <button
                onClick={() => setActiveSideTab('both')}
                className={`px-3 py-1 rounded ${activeSideTab === 'both' ? 'bg-slate-700 text-white' : 'text-slate-400'}`}
              >
                Dual Chain
              </button>
              <button
                onClick={() => setActiveSideTab('calls')}
                className={`px-3 py-1 rounded ${activeSideTab === 'calls' ? 'bg-blue-600 text-white' : 'text-slate-400'}`}
              >
                Calls Only
              </button>
              <button
                onClick={() => setActiveSideTab('puts')}
                className={`px-3 py-1 rounded ${activeSideTab === 'puts' ? 'bg-purple-600 text-white' : 'text-slate-400'}`}
              >
                Puts Only
              </button>
            </div>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-center text-xs">
              <thead className="bg-slate-800/80 text-slate-400 uppercase tracking-wider font-semibold border-b border-slate-700">
                <tr>
                  {(activeSideTab === 'both' || activeSideTab === 'calls') && (
                    <>
                      <th className="py-2.5 px-2 text-blue-400">Call Delta</th>
                      <th className="py-2.5 px-2 text-blue-400">Call IV</th>
                      <th className="py-2.5 px-2 text-blue-400">Call Bid</th>
                      <th className="py-2.5 px-2 text-blue-400">Call Ask</th>
                      <th className="py-2.5 px-2 text-blue-400">Call Mid</th>
                    </>
                  )}
                  <th className="py-2.5 px-4 bg-slate-800 text-white font-bold">Strike</th>
                  <th className="py-2.5 px-2 text-slate-400">DTE</th>
                  {(activeSideTab === 'both' || activeSideTab === 'puts') && (
                    <>
                      <th className="py-2.5 px-2 text-purple-400">Put Mid</th>
                      <th className="py-2.5 px-2 text-purple-400">Put Bid</th>
                      <th className="py-2.5 px-2 text-purple-400">Put Ask</th>
                      <th className="py-2.5 px-2 text-purple-400">Put IV</th>
                      <th className="py-2.5 px-2 text-purple-400">Put Delta</th>
                    </>
                  )}
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/60 font-mono text-slate-300">
                {loading ? (
                  <tr>
                    <td colSpan={12} className="py-10 text-center text-slate-500">Loading historical option chain...</td>
                  </tr>
                ) : strikes.length === 0 ? (
                  <tr>
                    <td colSpan={12} className="py-10 text-center text-slate-500">No options found for this date. Seed this ticker in Dashboard to populate.</td>
                  </tr>
                ) : (
                  strikes.map(strike => {
                    const call = callsByStrike.get(strike);
                    const put = putsByStrike.get(strike);
                    const isItmCall = strike < spot;
                    const isItmPut = strike > spot;

                    return (
                      <tr
                        key={strike}
                        onClick={() => {
                          if (call) {
                            setSelectedStrikePayoff(call.strike);
                            setSelectedPremiumPayoff(call.bid);
                          }
                        }}
                        className={`hover:bg-slate-800/50 cursor-pointer transition-colors ${
                          selectedStrikePayoff === strike ? 'bg-blue-900/20' : ''
                        }`}
                      >
                        {/* Call Side */}
                        {(activeSideTab === 'both' || activeSideTab === 'calls') && (
                          <>
                            <td className={`py-2 px-2 ${isItmCall ? 'bg-blue-950/30 text-blue-300 font-semibold' : 'text-slate-400'}`}>
                              {call?.delta ? call.delta.toFixed(2) : '-'}
                            </td>
                            <td className="py-2 px-2 text-slate-400">
                              {call?.impliedVolatility ? `${(call.impliedVolatility * 100).toFixed(1)}%` : '-'}
                            </td>
                            <td className="py-2 px-2">{call?.bid?.toFixed(2) || '-'}</td>
                            <td className="py-2 px-2">{call?.ask?.toFixed(2) || '-'}</td>
                            <td className="py-2 px-2 font-semibold text-emerald-400">{call?.mid?.toFixed(2) || '-'}</td>
                          </>
                        )}

                        {/* Strike Center */}
                        <td className={`py-2 px-4 font-bold ${
                          Math.abs(strike - spot) < 2.5 ? 'bg-emerald-950/40 text-emerald-300 border-x border-emerald-600/40' : 'bg-slate-800 text-white'
                        }`}>
                          ${strike.toFixed(2)}
                        </td>
                        <td className="py-2 px-2 text-slate-400 text-[11px]">{call?.dte || put?.dte || '-'}d</td>

                        {/* Put Side */}
                        {(activeSideTab === 'both' || activeSideTab === 'puts') && (
                          <>
                            <td className="py-2 px-2 font-semibold text-purple-400">{put?.mid?.toFixed(2) || '-'}</td>
                            <td className="py-2 px-2">{put?.bid?.toFixed(2) || '-'}</td>
                            <td className="py-2 px-2">{put?.ask?.toFixed(2) || '-'}</td>
                            <td className="py-2 px-2 text-slate-400">
                              {put?.impliedVolatility ? `${(put.impliedVolatility * 100).toFixed(1)}%` : '-'}
                            </td>
                            <td className={`py-2 px-2 ${isItmPut ? 'bg-purple-950/30 text-purple-300 font-semibold' : 'text-slate-400'}`}>
                              {put?.delta ? put.delta.toFixed(2) : '-'}
                            </td>
                          </>
                        )}
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

    </div>
  );
};
