import React, { useState } from 'react';
import { Navbar } from './components/Navbar';
import { DashboardPage } from './pages/DashboardPage';
import { TimeTravelExplorerPage } from './pages/TimeTravelExplorerPage';
import { DataIntegrityPage } from './pages/DataIntegrityPage';
import { BacktestStudioPage } from './pages/BacktestStudioPage';
import { ApiKeysPage } from './pages/ApiKeysPage';
import { MarketApi } from './services/api';

export function App() {
  const [activeTab, setActiveTab] = useState('dashboard');
  const [isHarvesting, setIsHarvesting] = useState(false);

  const handleQuickHarvest = async () => {
    try {
      setIsHarvesting(true);
      await MarketApi.runDailyHarvest();
      alert('Immediate EOD harvest triggered successfully across all active symbols!');
    } catch (err) {
      console.error('Quick harvest error:', err);
    } finally {
      setIsHarvesting(false);
    }
  };

  return (
    <div className="min-h-screen bg-[#0B0F19] text-slate-100 flex flex-col selection:bg-blue-600 selection:text-white">
      <Navbar
        activeTab={activeTab}
        setActiveTab={setActiveTab}
        onQuickHarvest={handleQuickHarvest}
        isHarvesting={isHarvesting}
      />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-6">
        {activeTab === 'dashboard' && <DashboardPage />}
        {activeTab === 'explorer' && <TimeTravelExplorerPage />}
        {activeTab === 'integrity' && <DataIntegrityPage />}
        {activeTab === 'backtest' && <BacktestStudioPage />}
        {activeTab === 'apikeys' && <ApiKeysPage />}
      </main>

      <footer className="border-t border-slate-800/80 bg-[#0B0F19] py-4 text-center text-xs text-slate-500">
        <div className="max-w-7xl mx-auto px-4 flex flex-col sm:flex-row items-center justify-between gap-2">
          <span>MyTradingToolbox-Backtest &copy; {new Date().getFullYear()} — Self-Hosted Market Data Vault</span>
          <div className="flex items-center space-x-4">
            <span className="text-emerald-400 font-mono">Tradier Free API: Connected ($0/mo)</span>
            <a href="http://localhost:5000/swagger" target="_blank" rel="noreferrer" className="text-blue-400 hover:underline">
              REST Swagger API
            </a>
          </div>
        </div>
      </footer>
    </div>
  );
}

export default App;
