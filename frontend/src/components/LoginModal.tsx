import React, { useState } from 'react';
import { GoogleLogin } from '@react-oauth/google';
import { useAuth } from '../context/AuthContext';
import { ShieldCheck, KeyRound, AlertCircle, Lock } from 'lucide-react';

export const LoginModal: React.FC = () => {
  const {
    loginModalOpen,
    closeLoginModal,
    loginWithGoogle,
    verifyTwoFactor,
    requiresTwoFactorChallenge
  } = useAuth();

  const [totpCode, setTotpCode] = useState('');
  const [errorMsg, setErrorMsg] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);

  if (!loginModalOpen) return null;

  const handleGoogleSuccess = async (response: any) => {
    setErrorMsg('');
    if (response.credential) {
      const res = await loginWithGoogle(response.credential);
      if (!res.success && res.message) {
        setErrorMsg(res.message);
      }
    }
  };

  const handleTotpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (totpCode.length !== 6) {
      setErrorMsg('Please enter a valid 6-digit code');
      return;
    }

    setIsSubmitting(true);
    setErrorMsg('');
    const success = await verifyTwoFactor(totpCode);
    setIsSubmitting(false);

    if (!success) {
      setErrorMsg('Invalid or expired 2FA code. Please try again.');
    }
  };

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
      <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-md p-6 shadow-2xl relative animate-in fade-in zoom-in duration-200">
        <button
          onClick={closeLoginModal}
          className="absolute top-4 right-4 text-slate-400 hover:text-white transition-colors"
        >
          ✕
        </button>

        {!requiresTwoFactorChallenge ? (
          <div className="text-center py-4">
            <div className="inline-flex p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-2xl text-emerald-400 mb-4">
              <Lock className="w-8 h-8" />
            </div>
            <h2 className="text-2xl font-bold text-white mb-2">MyTradingToolbox Vault</h2>
            <p className="text-sm text-slate-400 mb-6">
              Sign in with your authorized Google Account to manage historical market data, automated harvesters, and backtest execution.
            </p>

            {errorMsg && (
              <div className="mb-4 p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-rose-400 text-xs flex items-center justify-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{errorMsg}</span>
              </div>
            )}

            <div className="flex justify-center py-2">
              <GoogleLogin
                onSuccess={handleGoogleSuccess}
                onError={() => setErrorMsg('Google Sign-In failed or was cancelled.')}
                theme="filled_black"
                shape="pill"
                size="large"
              />
            </div>

            <p className="text-xs text-slate-500 mt-6">
              Protected by 256-bit AES encryption & optional Two-Factor Authentication (TOTP).
            </p>
          </div>
        ) : (
          <div className="py-2">
            <div className="text-center mb-6">
              <div className="inline-flex p-3 bg-indigo-500/10 border border-indigo-500/30 rounded-2xl text-indigo-400 mb-3">
                <ShieldCheck className="w-8 h-8" />
              </div>
              <h2 className="text-xl font-bold text-white">Two-Factor Authentication</h2>
              <p className="text-xs text-slate-400 mt-1">
                Enter the 6-digit verification code from your Google Authenticator or 1Password app.
              </p>
            </div>

            {errorMsg && (
              <div className="mb-4 p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-rose-400 text-xs flex items-center gap-2">
                <AlertCircle className="w-4 h-4 shrink-0" />
                <span>{errorMsg}</span>
              </div>
            )}

            <form onSubmit={handleTotpSubmit} className="space-y-4">
              <div>
                <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-2">
                  Authenticator Code
                </label>
                <div className="relative">
                  <KeyRound className="w-5 h-5 absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    maxLength={6}
                    autoFocus
                    placeholder="000000"
                    value={totpCode}
                    onChange={(e) => setTotpCode(e.target.value.replace(/\D/g, ''))}
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl py-3 pl-10 pr-4 text-center text-2xl tracking-[0.4em] font-mono text-emerald-400 focus:outline-none focus:border-emerald-500 transition-colors"
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={totpCode.length !== 6 || isSubmitting}
                className="w-full py-3 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 disabled:cursor-not-allowed text-white font-semibold rounded-xl transition-colors shadow-lg shadow-emerald-950 flex items-center justify-center gap-2"
              >
                {isSubmitting ? 'Verifying...' : 'Verify & Sign In'}
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
};
