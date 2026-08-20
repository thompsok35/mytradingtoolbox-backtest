import React, { useState, useEffect } from 'react';
import { GoogleOAuthProvider, GoogleLogin } from '@react-oauth/google';
import { AuthProvider, useAuth } from './context/AuthContext';
import { Navbar } from './components/Navbar';
import { LoginModal } from './components/LoginModal';
import { TwoFactorSetupModal } from './components/TwoFactorSetupModal';
import { DashboardPage } from './pages/DashboardPage';
import { TimeTravelExplorerPage } from './pages/TimeTravelExplorerPage';
import { DataIntegrityPage } from './pages/DataIntegrityPage';
import { BacktestStudioPage } from './pages/BacktestStudioPage';
import { ApiKeysPage } from './pages/ApiKeysPage';
import { TroubleshootingPage } from './pages/TroubleshootingPage';
import { AuthApi } from './services/api';
import { Lock, Shield, Database, PlaySquare, Key, Activity, RefreshCw } from 'lucide-react';

export const AppContent: React.FC = () => {
  const [activeTab, setActiveTab] = useState('dashboard');
  const { user, isLoading, loginWithGoogle } = useAuth();
  const [loginError, setLoginError] = useState('');

  // 1. Initial Session Verification
  if (isLoading) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col items-center justify-center p-4">
        <div className="flex flex-col items-center gap-3">
          <RefreshCw className="w-8 h-8 text-emerald-400 animate-spin" />
          <p className="text-sm font-mono text-slate-400">Verifying secure session...</p>
        </div>
      </div>
    );
  }

  // 2. Strict Authentication Wall: If unauthenticated, show Lock Screen Gate
  if (!user) {
    return (
      <div className="min-h-screen bg-slate-950 text-slate-100 flex flex-col font-sans selection:bg-emerald-500 selection:text-slate-950">
        <Navbar activeTab={activeTab} setActiveTab={setActiveTab} />

        <main className="flex-1 max-w-4xl w-full mx-auto px-4 sm:px-6 lg:px-8 py-12 flex flex-col items-center justify-center">
          <div className="bg-slate-900 border border-slate-800 rounded-3xl p-8 sm:p-12 shadow-2xl text-center max-w-xl w-full">
            <div className="inline-flex p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-2xl text-emerald-400 mb-6 shadow-inner">
              <Lock className="w-10 h-10" />
            </div>

            <h1 className="text-2xl sm:text-3xl font-extrabold text-white tracking-tight mb-3">
              Protected Market Vault
            </h1>
            <p className="text-sm text-slate-400 leading-relaxed mb-8">
              Access to historical option chains, automated ingestion pipelines, gap-repair auditors, and the backtesting studio is restricted to authorized traders.
            </p>

            {loginError && (
              <div className="mb-6 p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-rose-400 text-xs text-center">
                {loginError}
              </div>
            )}

            <div className="flex justify-center mb-8">
              <GoogleLogin
                onSuccess={async (credentialResponse) => {
                  setLoginError('');
                  if (credentialResponse.credential) {
                    const res = await loginWithGoogle(credentialResponse.credential);
                    if (!res.success && res.message) {
                      setLoginError(res.message);
                    }
                  }
                }}
                onError={() => setLoginError('Google Sign-In failed or was cancelled.')}
                theme="filled_black"
                shape="pill"
                size="large"
              />
            </div>

            {/* Protected Modules Feature List */}
            <div className="grid grid-cols-2 gap-3 text-left pt-6 border-t border-slate-800 text-xs">
              <div className="flex items-center gap-2 text-slate-300">
                <Database className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>PostgreSQL Option Vault</span>
              </div>
              <div className="flex items-center gap-2 text-slate-300">
                <PlaySquare className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Backtest Studio Engine</span>
              </div>
              <div className="flex items-center gap-2 text-slate-300">
                <Shield className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>2FA TOTP Protection</span>
              </div>
              <div className="flex items-center gap-2 text-slate-300">
                <Key className="w-4 h-4 text-emerald-400 shrink-0" />
                <span>Machine API Key Manager</span>
              </div>
            </div>
          </div>
        </main>

        <footer className="border-t border-slate-800/80 bg-slate-900/40 py-6 text-center text-xs text-slate-500">
          <p>© 2026 MyTradingToolbox Suite • Secure Vault & Backtesting Engine</p>
        </footer>

        {/* Global Modals (for 2FA challenge when logging in) */}
        <LoginModal />
        <TwoFactorSetupModal />
      </div>
    );
  }

  // 3. Authenticated User Workspace
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
  const [clientId, setClientId] = useState<string>(() => {
    return (
      (typeof import.meta !== 'undefined' &&
        (import.meta.env.VITE_GOOGLE_CLIENT_ID ||
          import.meta.env.GOOGLE_CLIENT_ID)) ||
      ''
    );
  });

  useEffect(() => {
    const fetchConfig = async () => {
      try {
        const config = await AuthApi.getConfig();
        if (config.googleClientId) {
          setClientId(config.googleClientId.trim());
        }
      } catch (err) {
        console.warn('Could not fetch dynamic auth config:', err);
      }
    };
    fetchConfig();
  }, []);

  const activeClientId = clientId || '1234567890-placeholder.apps.googleusercontent.com';

  return (
    <GoogleOAuthProvider clientId={activeClientId}>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </GoogleOAuthProvider>
  );
}

export default App;
