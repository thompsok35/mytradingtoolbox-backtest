import React from 'react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  CartesianGrid,
  ReferenceLine
} from 'recharts';
import { OptionContractDto } from '../types';

interface VolatilitySkewChartProps {
  calls: OptionContractDto[];
  puts: OptionContractDto[];
  underlyingPrice: number;
}

export const VolatilitySkewChart: React.FC<VolatilitySkewChartProps> = ({
  calls,
  puts,
  underlyingPrice
}) => {
  // Merge call and put IV by strike
  const map = new Map<number, { strike: number; callIv?: number; putIv?: number }>();

  calls.forEach(c => {
    if (c.impliedVolatility && c.impliedVolatility > 0) {
      const existing = map.get(c.strike) || { strike: c.strike };
      existing.callIv = Math.round(c.impliedVolatility * 1000) / 10; // in %
      map.set(c.strike, existing);
    }
  });

  puts.forEach(p => {
    if (p.impliedVolatility && p.impliedVolatility > 0) {
      const existing = map.get(p.strike) || { strike: p.strike };
      existing.putIv = Math.round(p.impliedVolatility * 1000) / 10;
      map.set(p.strike, existing);
    }
  });

  const data = Array.from(map.values()).sort((a, b) => a.strike - b.strike);

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
      <div className="flex items-center justify-between mb-3">
        <div>
          <h4 className="text-xs font-semibold text-slate-200 uppercase tracking-wider">
            Implied Volatility Smile & Skew Curve
          </h4>
          <p className="text-xs text-slate-400">Strike vs Mid Implied Volatility (%)</p>
        </div>
        <div className="flex items-center space-x-3 text-xs">
          <span className="flex items-center space-x-1">
            <span className="w-2.5 h-2.5 rounded-full bg-blue-500 inline-block"></span>
            <span className="text-slate-300">Call IV</span>
          </span>
          <span className="flex items-center space-x-1">
            <span className="w-2.5 h-2.5 rounded-full bg-purple-500 inline-block"></span>
            <span className="text-slate-300">Put IV</span>
          </span>
        </div>
      </div>

      <div className="h-56 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1E293B" />
            <XAxis dataKey="strike" stroke="#64748B" tick={{ fontSize: 10 }} tickFormatter={v => `$${v}`} />
            <YAxis stroke="#64748B" tick={{ fontSize: 10 }} tickFormatter={v => `${v}%`} />
            <Tooltip
              contentStyle={{ backgroundColor: '#0F172A', borderColor: '#334155', borderRadius: '8px', fontSize: '12px' }}
              formatter={(val: any) => [`${val}%`, 'IV']}
              labelFormatter={label => `Strike: $${label}`}
            />
            <ReferenceLine x={underlyingPrice} stroke="#10B981" strokeDasharray="3 3" label={{ value: 'ATM', fill: '#10B981', fontSize: 10, position: 'top' }} />
            <Line type="monotone" dataKey="callIv" stroke="#3B82F6" strokeWidth={2} dot={{ r: 2 }} name="Call IV" />
            <Line type="monotone" dataKey="putIv" stroke="#A855F7" strokeWidth={2} dot={{ r: 2 }} name="Put IV" />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};
