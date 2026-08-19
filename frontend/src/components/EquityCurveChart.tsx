import React from 'react';
import {
  ResponsiveContainer,
  AreaChart,
  Area,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  Legend
} from 'recharts';
import { EquityPoint } from '../types';

interface EquityCurveChartProps {
  data: EquityPoint[];
}

export const EquityCurveChart: React.FC<EquityCurveChartProps> = ({ data }) => {
  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="text-sm font-semibold text-slate-200">Portfolio Equity Curve vs Benchmark</h3>
          <p className="text-xs text-slate-400">Mark-to-market daily performance of ITM Covered Call strategy</p>
        </div>
        <div className="flex items-center space-x-4 text-xs font-mono">
          <div className="flex items-center space-x-1.5">
            <span className="w-3 h-3 rounded-full bg-emerald-500 inline-block"></span>
            <span className="text-slate-300">Strategy Total Equity</span>
          </div>
          <div className="flex items-center space-x-1.5">
            <span className="w-3 h-3 rounded-full bg-blue-500 inline-block"></span>
            <span className="text-slate-300">Buy & Hold Benchmark</span>
          </div>
        </div>
      </div>

      <div className="h-72 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <AreaChart data={data} margin={{ top: 10, right: 10, left: 10, bottom: 0 }}>
            <defs>
              <linearGradient id="strategyGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#10B981" stopOpacity={0.3} />
                <stop offset="95%" stopColor="#10B981" stopOpacity={0.0} />
              </linearGradient>
              <linearGradient id="benchmarkGrad" x1="0" y1="0" x2="0" y2="1">
                <stop offset="5%" stopColor="#3B82F6" stopOpacity={0.15} />
                <stop offset="95%" stopColor="#3B82F6" stopOpacity={0.0} />
              </linearGradient>
            </defs>
            <CartesianGrid strokeDasharray="3 3" stroke="#1E293B" />
            <XAxis dataKey="date" stroke="#64748B" tick={{ fontSize: 10 }} />
            <YAxis stroke="#64748B" tick={{ fontSize: 10 }} tickFormatter={v => `$${(v / 1000).toFixed(1)}k`} domain={['auto', 'auto']} />
            <Tooltip
              contentStyle={{ backgroundColor: '#0F172A', borderColor: '#334155', borderRadius: '8px', fontSize: '12px' }}
              formatter={(val: any, name: any) => [
                `$${Number(val).toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`,
                name === 'totalEquity' ? 'Strategy Equity' : 'Buy & Hold'
              ]}
              labelFormatter={label => `Date: ${label}`}
            />
            <Area
              type="monotone"
              dataKey="totalEquity"
              stroke="#10B981"
              strokeWidth={2.5}
              fillOpacity={1}
              fill="url(#strategyGrad)"
              name="totalEquity"
            />
            <Line
              type="monotone"
              dataKey="benchmarkEquity"
              stroke="#3B82F6"
              strokeWidth={1.8}
              strokeDasharray="4 4"
              dot={false}
              name="benchmarkEquity"
            />
          </AreaChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};
