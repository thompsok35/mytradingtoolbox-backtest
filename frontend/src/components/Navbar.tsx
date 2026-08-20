import React, { useState } from 'react';
import { 
  Database, 
  Calendar, 
  ShieldCheck, 
  PlaySquare, 
  Key, 
  TrendingUp, 
  Activity, 
  LogIn, 
  LogOut, 
  ShieldAlert,
  Lock
} from 'lucide-react';
import { useAuth } from '../context/AuthContext';

interface NavbarProps {
  activeTab: string;
  setActiveTab: (tab: string) => void;
}

export const Navbar: React.FC<NavbarProps> = ({ activeTab, setActiveTab }) => {
  const { user, openLoginModal, openTwoFactorSetup, logout } = useAuth();
  const [profileDropdownOpen, setProfileDropdownOpen] = useState(false);

  const navItems = [
    { id: 'dashboard', label: 'Watchlist & Vault', icon: Database },
    { id: 'timetravel', label: 'Time-Travel Chain', icon: Calendar },
    { id: 'integrity', label: 'Quality & Integrity', icon: ShieldCheck },
    { id: 'backtest', label: 'Backtest Studio', icon: PlaySquare },
    { id: 'apikeys', label: 'API Keys & Logs', icon: Key },
    { id: 'troubleshooting', label: 'Troubleshooting', icon: Activity },
  ];

  return (
    <header className="bg-slate-900/90 backdrop-blur-md border-b border-slate-800 sticky top-0 z-40">
      <div className="max-w-7xl mx-auto px-4 sm:px-6 lg:px-8">
        <div className="flex items-center justify-between h-16">
          {/* Brand Logo */}
          <div className="flex items-center gap-3">
            <div className="bg-gradient-to-tr from-emerald-500 to-teal-400 p-2.5 rounded-xl shadow-lg shadow-emerald-950">
              <TrendingUp className="w-5 h-5 text-slate-950 font-black" />
            </div>
            <div>
              <div className="flex items-center gap-2">
                <span className="text-lg font-bold text-white tracking-tight">MyTradingToolbox</span>
                <span className="text-[10px] uppercase font-semibold bg-emerald-500/20 text-emerald-400 px-2 py-0.5 rounded border border-emerald-500/30">
                  Vault v1.0
                </span>
              </div>
              <p className="text-[11px] text-slate-400">Historical Options & Backtest Engine</p>
            </div>
          </div>

          {/* Navigation Links - Only visible when Authenticated */}
          {user ? (
            <nav className="hidden lg:flex items-center gap-1 bg-slate-950/60 p-1 rounded-xl border border-slate-800/80">
              {navItems.map((item) => {
                const Icon = item.icon;
                const isActive = activeTab === item.id;
                return (
                  <button
                    key={item.id}
                    onClick={() => setActiveTab(item.id)}
                    className={`flex items-center gap-2 px-3.5 py-1.5 rounded-lg text-xs font-medium transition-all ${
                      isActive
                        ? 'bg-emerald-500 text-slate-950 font-semibold shadow-md shadow-emerald-950'
                        : 'text-slate-400 hover:text-white hover:bg-slate-800/50'
                    }`}
                  >
                    <Icon className="w-3.5 h-3.5" />
                    {item.label}
                  </button>
                );
              })}
            </nav>
          ) : (
            <div className="hidden sm:flex items-center gap-2 text-xs text-slate-400 bg-slate-950/40 px-3 py-1.5 rounded-xl border border-slate-800">
              <Lock className="w-3.5 h-3.5 text-amber-400" />
              <span>Authentication Required to Access Data</span>
            </div>
          )}

          {/* User Auth Section */}
          <div className="flex items-center gap-3">
            {user ? (
              <div className="relative">
                <button
                  onClick={() => setProfileDropdownOpen(!profileDropdownOpen)}
                  className="flex items-center gap-2.5 bg-slate-800/80 hover:bg-slate-800 border border-slate-700/80 p-1.5 pr-3 rounded-xl transition-colors"
                >
                  {user.pictureUrl ? (
                    <img src={user.pictureUrl} alt={user.name} className="w-7 h-7 rounded-lg object-cover" />
                  ) : (
                    <div className="w-7 h-7 rounded-lg bg-emerald-600 text-white flex items-center justify-center font-bold text-xs">
                      {user.name.charAt(0)}
                    </div>
                  )}
                  <div className="text-left hidden sm:block">
                    <p className="text-xs font-semibold text-white leading-tight">{user.name}</p>
                    <div className="flex items-center gap-1">
                      {user.isTwoFactorEnabled ? (
                        <span className="text-[10px] text-emerald-400 flex items-center gap-0.5">
                          <ShieldCheck className="w-2.5 h-2.5" /> 2FA Active
                        </span>
                      ) : (
                        <span className="text-[10px] text-amber-400 flex items-center gap-0.5">
                          <ShieldAlert className="w-2.5 h-2.5" /> No 2FA
                        </span>
                      )}
                    </div>
                  </div>
                </button>

                {/* Profile Dropdown */}
                {profileDropdownOpen && (
                  <div className="absolute right-0 mt-2 w-56 bg-slate-900 border border-slate-700 rounded-2xl p-2 shadow-2xl z-50 animate-in fade-in zoom-in-95 duration-150">
                    <div className="px-3 py-2 border-b border-slate-800">
                      <p className="text-xs font-semibold text-white truncate">{user.name}</p>
                      <p className="text-[11px] text-slate-400 truncate">{user.email}</p>
                    </div>

                    <div className="py-1">
                      <button
                        onClick={() => {
                          setProfileDropdownOpen(false);
                          openTwoFactorSetup();
                        }}
                        className="w-full text-left flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-slate-300 hover:text-white hover:bg-slate-800 rounded-lg transition-colors"
                      >
                        <ShieldCheck className="w-4 h-4 text-emerald-400" />
                        {user.isTwoFactorEnabled ? 'Manage 2FA Settings' : 'Enable 2FA (TOTP)'}
                      </button>
                    </div>

                    <div className="pt-1 border-t border-slate-800">
                      <button
                        onClick={() => {
                          setProfileDropdownOpen(false);
                          logout();
                        }}
                        className="w-full text-left flex items-center gap-2.5 px-3 py-2 text-xs font-medium text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
                      >
                        <LogOut className="w-4 h-4" />
                        Sign Out
                      </button>
                    </div>
                  </div>
                )}
              </div>
            ) : (
              <button
                onClick={openLoginModal}
                className="flex items-center gap-2 px-4 py-2 bg-emerald-600 hover:bg-emerald-500 text-white text-xs font-semibold rounded-xl shadow-lg shadow-emerald-950 transition-colors"
              >
                <LogIn className="w-4 h-4" />
                Sign In
              </button>
            )}
          </div>
        </div>
      </div>
    </header>
  );
};
