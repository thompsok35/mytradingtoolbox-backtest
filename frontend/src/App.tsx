import React, { useState } from 'react';
import { GoogleOAuthProvider } from '@react-oauth/google';
import { AuthProvider } from './context/AuthContext';
import { Navbar } from './components/Navbar';
import { LoginModal } from './components/LoginModal';
import { TwoFactorSetupModal } from './components/TwoFactorSetupModal';
import { DashboardPage } from './pages/DashboardPage';
import { TimeTravelExplorerPage } from './pages/TimeTravelExplorerPage';
import { DataIntegrityPage } from './pages/DataIntegrityPage';
import { BacktestStudioPage } from './pages/BacktestStudioPage';
import { ApiKeysPage } from './pages/ApiKeysPage';
import { TroubleshootingPage } from './pages/TroubleshootingPage';

export const AppContent: React.FC = () => {
  const [activeTab, setActiveTab] = useState('dashboard');

  return (
    <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-emerald-500 selection:text-slate-950">
      <Navbar activeTab={activeTab} setActiveTab={setActiveTab} />

      <main className="flex-1 max-w-7xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-8">
        {activeTab === 'dashboard' && <DashboardPage />}
        {activeTab === 'timetravel' && <TimeTravelExplorerPage />}
        {activeTab === 'integrity' && <DataIntegrityPage />}
        {activeTab === 'backtest' && <BacktestStudioPage />}
        {activeTab === 'apikeys' && <ApiKeysPage />}
        {activeTab === 'troubleshooting' && <TroubleshootingPage />}
      </main>

      <footer className="border-t border-slate-800/80 bg-slate-900/40 py-6 text-center text-xs text-slate-500">
        <p>© 2026 MyTradingToolbox Suite • High-Performance PostgreSQL Vault & Backtesting Engine • Standardized REST API</p>
      </footer>

      {/* Global Modals */}
      <LoginModal />
      <TwoFactorSetupModal />
    </div>
  );
};

export function App() {
  const googleClientId =
    (typeof import.meta !== 'undefined' && (import.meta.env.VITE_GOOGLE_CLIENT_ID || import.meta.env.GOOGLE_CLIENT_ID)) ||
    '1234567890-placeholder.apps.googleusercontent.com';

  return (
    <GoogleOAuthProvider clientId={googleClientId}>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </GoogleOAuthProvider>
  );
}

export default App;
