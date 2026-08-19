import React from 'react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ReferenceLine,
  CartesianGrid
} from 'recharts';

interface PayoffChartProps {
  spotPrice: number;
  strikePrice: number;
  premiumReceived: number;
  contracts?: number;
}

export const PayoffChart: React.FC<PayoffChartProps> = ({
  spotPrice,
  strikePrice,
  premiumReceived,
  contracts = 1
}) => {
  // Generate range of prices at expiration (-15% to +15% around spot)
  const data = [];
  const minPrice = Math.round(spotPrice * 0.85);
  const maxPrice = Math.round(spotPrice * 1.15);
  const step = Math.max(1, Math.round((maxPrice - minPrice) / 30));

  const netDebit = spotPrice - premiumReceived;
  const maxProfitPerShare = strikePrice - netDebit;
  const breakEven = netDebit;

  for (let p = minPrice; p <= maxPrice; p += step) {
    let pnlPerShare = 0;
    if (p >= strikePrice) {
      pnlPerShare = maxProfitPerShare;
    } else {
      pnlPerShare = p - netDebit;
    }

    const totalPnl = pnlPerShare * 100 * contracts;

    data.push({
      price: p,
      pnl: Math.round(totalPnl),
      buyAndHoldPnl: Math.round((p - spotPrice) * 100 * contracts)
    });
  }

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-4 shadow-lg">
      <div className="flex items-center justify-between mb-3">
        <div>
          <h4 className="text-xs font-semibold text-slate-200 uppercase tracking-wider">
            Covered Call Expiration Payoff
          </h4>
          <p className="text-xs text-slate-400">
            Strike: ${strikePrice.toFixed(2)} | Net Debit: ${netDebit.toFixed(2)} | Max Profit: ${(maxProfitPerShare * 100 * contracts).toFixed(2)}
          </p>
        </div>
        <div className="text-right text-xs font-mono">
          <span className="text-emerald-400">Break-Even: ${breakEven.toFixed(2)}</span>
        </div>
      </div>

      <div className="h-56 w-full">
        <ResponsiveContainer width="100%" height="100%">
          <LineChart data={data} margin={{ top: 10, right: 10, left: -20, bottom: 0 }}>
            <CartesianGrid strokeDasharray="3 3" stroke="#1E293B" />
            <XAxis dataKey="price" stroke="#64748B" tick={{ fontSize: 10 }} tickFormatter={v => `$${v}`} />
            <YAxis stroke="#64748B" tick={{ fontSize: 10 }} tickFormatter={v => `$${v}`} />
            <Tooltip
              contentStyle={{ backgroundColor: '#0F172A', borderColor: '#334155', borderRadius: '8px', fontSize: '12px' }}
              formatter={(val: any, name: any) => [
                `$${val}`,
                name === 'pnl' ? 'Covered Call P&L' : 'Stock Buy & Hold'
              ]}
              labelFormatter={label => `Stock Price at Expiration: $${label}`}
            />
            <ReferenceLine y={0} stroke="#475569" strokeDasharray="3 3" />
            <ReferenceLine x={spotPrice} stroke="#3B82F6" strokeDasharray="2 2" label={{ value: 'Spot', fill: '#3B82F6', fontSize: 10, position: 'top' }} />
            <ReferenceLine x={strikePrice} stroke="#F59E0B" strokeDasharray="2 2" label={{ value: 'Strike', fill: '#F59E0B', fontSize: 10, position: 'top' }} />
            <Line type="monotone" dataKey="pnl" stroke="#10B981" strokeWidth={2.5} dot={false} name="pnl" />
            <Line type="monotone" dataKey="buyAndHoldPnl" stroke="#64748B" strokeWidth={1.5} strokeDasharray="4 4" dot={false} name="buyAndHoldPnl" />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </div>
  );
};
