import React from 'react';
import { Database, Activity, Calendar, PlaySquare, Key, RefreshCw, ExternalLink, ShieldCheck } from 'lucide-react';

interface NavbarProps {
  activeTab: string;
  setActiveTab: (tab: string) => void;
  onQuickHarvest: () => void;
  isHarvesting: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  setActiveTab,
  onQuickHarvest,
  isHarvesting
}) => {
  const navItems = [
    { id: 'dashboard', label: 'Watchlist & Vault', icon: Database },
    { id: 'explorer', label: 'Time-Travel Chain', icon: Calendar },
    { id: 'integrity', label: 'Quality & Integrity', icon: ShieldCheck },
    { id: 'backtest', label: 'Backtest Studio', icon: PlaySquare },
    { id: 'apikeys', label: 'API Keys & Logs', icon: Key },
  ];

  return (
    <header className="sticky top-0 z-40 bg-[#0F172A]/90 backdrop-blur-md border-b border-slate-800">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          
          {/* Logo & Branding */}
          <div className="flex items-center space-x-3 cursor-pointer" onClick={() => setActiveTab('dashboard')}>
            <div className="w-10 h-10 rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-500 flex items-center justify-center shadow-lg shadow-blue-500/20 border border-blue-400/30">
              <Activity className="w-5 h-5 text-white" />
            </div>
            <div>
              <div className="flex items-center space-x-2">
                <span className="font-bold text-lg bg-gradient-to-r from-white via-slate-100 to-slate-400 bg-clip-text text-transparent">
                  MyTradingToolbox
                </span>
                <span className="text-xs px-2 py-0.5 rounded-full bg-blue-500/10 border border-blue-500/30 text-blue-400 font-semibold">
                  Vault & Backtest
                </span>
              </div>
              <p className="text-xs text-slate-400">Perpetual EOD Harvester & Engine</p>
            </div>
          </div>

          {/* Navigation Links */}
          <nav className="hidden md:flex items-center space-x-1 bg-slate-900/60 p-1.5 rounded-xl border border-slate-800">
            {navItems.map(item => {
              const Icon = item.icon;
              const isActive = activeTab === item.id;
              return (
                <button
                  key={item.id}
                  onClick={() => setActiveTab(item.id)}
                  className={`flex items-center space-x-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
                    isActive
                      ? 'bg-blue-600 text-white shadow-md shadow-blue-600/30'
                      : 'text-slate-400 hover:text-slate-200 hover:bg-slate-800/60'
                  }`}
                >
                  <Icon className="w-4 h-4" />
                  <span>{item.label}</span>
                </button>
              );
            })}
          </nav>

          {/* Actions & Status */}
          <div className="flex items-center space-x-3">
            <button
              onClick={onQuickHarvest}
              disabled={isHarvesting}
              className="flex items-center space-x-1.5 px-3 py-1.5 rounded-lg bg-emerald-600/20 hover:bg-emerald-600/30 border border-emerald-500/40 text-emerald-400 text-xs font-medium transition-all disabled:opacity-50"
              title="Run immediate EOD harvest on all active watchlist tickers"
            >
              <RefreshCw className={`w-3.5 h-3.5 ${isHarvesting ? 'animate-spin' : ''}`} />
              <span>{isHarvesting ? 'Harvesting...' : 'Harvest Today'}</span>
            </button>

            <a
              href="http://localhost:5000/swagger"
              target="_blank"
              rel="noreferrer"
              className="flex items-center space-x-1 text-slate-400 hover:text-white text-xs px-2.5 py-1.5 rounded-lg bg-slate-800/80 hover:bg-slate-800 border border-slate-700 transition-all"
            >
              <span>Swagger</span>
              <ExternalLink className="w-3 h-3 ml-0.5" />
            </a>
          </div>

        </div>
      </div>
    </header>
  );
};
