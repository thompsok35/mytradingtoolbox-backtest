import React from 'react';

interface HeatmapCalendarProps {
  availableDates: string[];
  missingDates: string[];
  totalExpectedDays: number;
  healthScore: number;
}

export const HeatmapCalendar: React.FC<HeatmapCalendarProps> = ({
  availableDates,
  missingDates,
  totalExpectedDays,
  healthScore
}) => {
  const availableSet = new Set(availableDates);
  const missingSet = new Set(missingDates);

  // Generate last 6 months dates grid
  const days: { dateStr: string; status: 'present' | 'missing' | 'none' }[] = [];
  const today = new Date();
  const start = new Date();
  start.setMonth(start.getMonth() - 6);

  for (let d = new Date(start); d <= today; d.setDate(d.getDate() + 1)) {
    const isWeekend = d.getDay() === 0 || d.getDay() === 6;
    const dateStr = d.toISOString().split('T')[0];

    if (isWeekend) {
      days.push({ dateStr, status: 'none' });
    } else if (availableSet.has(dateStr)) {
      days.push({ dateStr, status: 'present' });
    } else if (missingSet.has(dateStr)) {
      days.push({ dateStr, status: 'missing' });
    } else {
      days.push({ dateStr, status: 'none' });
    }
  }

  return (
    <div className="bg-slate-900 border border-slate-800 rounded-xl p-5 shadow-lg">
      <div className="flex items-center justify-between mb-4">
        <div>
          <h3 className="text-sm font-semibold text-slate-200">Historical Coverage Calendar (Last 6 Months)</h3>
          <p className="text-xs text-slate-400">Green = Option Chain Stored, Red = Missing Trading Session Gap</p>
        </div>
        <div className="flex items-center space-x-4 text-xs">
          <div className="flex items-center space-x-1.5">
            <span className="w-3 h-3 rounded-sm bg-emerald-500 inline-block"></span>
            <span className="text-slate-300">Available ({availableDates.length})</span>
          </div>
          <div className="flex items-center space-x-1.5">
            <span className="w-3 h-3 rounded-sm bg-rose-500 inline-block"></span>
            <span className="text-slate-300">Missing ({missingDates.length})</span>
          </div>
          <div className="px-2.5 py-1 rounded bg-slate-800 text-blue-400 font-mono font-medium">
            Health: {healthScore.toFixed(1)}%
          </div>
        </div>
      </div>

      <div className="grid grid-flow-col grid-rows-7 gap-1.5 overflow-x-auto py-2">
        {days.map((day, idx) => (
          <div
            key={idx}
            title={`${day.dateStr}: ${day.status === 'present' ? 'Data Vaulted' : day.status === 'missing' ? 'Missing Gap' : 'Non-trading'}`}
            className={`w-3.5 h-3.5 rounded-sm transition-all ${
              day.status === 'present'
                ? 'bg-emerald-500 shadow-sm shadow-emerald-500/40 hover:scale-125'
                : day.status === 'missing'
                ? 'bg-rose-500/80 hover:scale-125'
                : 'bg-slate-800/40 hover:bg-slate-700/50'
            }`}
          />
        ))}
      </div>
    </div>
  );
};
