import React, { useState, useEffect } from 'react';
import { QRCodeSVG } from 'qrcode.react';
import { useAuth } from '../context/AuthContext';
import { AuthApi } from '../services/api';
import { ShieldCheck, Copy, Check, AlertCircle, KeyRound } from 'lucide-react';

export const TwoFactorSetupModal: React.FC = () => {
  const { twoFactorSetupOpen, closeTwoFactorSetup, refreshUser, user } = useAuth();
  const [setupData, setSetupData] = useState<{ secretKey: string; qrCodeUri: string; manualKey: string } | null>(null);
  const [code, setCode] = useState('');
  const [copied, setCopied] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [successMsg, setSuccessMsg] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    if (twoFactorSetupOpen && !user?.isTwoFactorEnabled) {
      loadSetupData();
    }
  }, [twoFactorSetupOpen]);

  const loadSetupData = async () => {
    try {
      const data = await AuthApi.setupTwoFactor();
      setSetupData({ secretKey: data.secretKey, qrCodeUri: data.qrCodeUri, manualKey: data.manualEntryKey || data.secretKey });
      setErrorMsg('');
      setSuccessMsg('');
    } catch {
      setErrorMsg('Failed to initialize 2FA setup.');
    }
  };

  if (!twoFactorSetupOpen) return null;

  const handleCopy = () => {
    if (setupData?.secretKey) {
      navigator.clipboard.writeText(setupData.secretKey);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    }
  };

  const handleVerifyAndEnable = async (e: React.FormEvent) => {
    e.preventDefault();
    if (code.length !== 6) {
      setErrorMsg('Please enter a 6-digit code');
      return;
    }

    setIsLoading(true);
    setErrorMsg('');
    try {
      const res = await AuthApi.verifyTwoFactor({ code });
      if (res.success) {
        setSuccessMsg('Two-Factor Authentication is now ENABLED!');
        await refreshUser();
        setTimeout(() => {
          closeTwoFactorSetup();
        }, 1500);
      } else {
        setErrorMsg(res.message || 'Invalid code.');
      }
    } catch (err: any) {
      setErrorMsg(err?.response?.data?.message || 'Invalid code.');
    } finally {
      setIsLoading(false);
    }
  };

  const handleDisable2FA = async () => {
    if (!confirm('Are you sure you want to disable Two-Factor Authentication?')) return;
    setIsLoading(true);
    try {
      await AuthApi.disableTwoFactor();
      await refreshUser();
      closeTwoFactorSetup();
    } catch {
      setErrorMsg('Failed to disable 2FA.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/80 backdrop-blur-sm z-50 flex items-center justify-center p-4">
      <div className="bg-slate-900 border border-slate-700 rounded-2xl w-full max-w-lg p-6 shadow-2xl relative">
        <button
          onClick={closeTwoFactorSetup}
          className="absolute top-4 right-4 text-slate-400 hover:text-white transition-colors"
        >
          ?
        </button>

        <div className="flex items-center gap-3 mb-4">
          <div className="p-2.5 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-emerald-400">
            <ShieldCheck className="w-6 h-6" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-white">Two-Factor Authentication (2FA)</h2>
            <p className="text-xs text-slate-400">RFC 6238 Time-based One-Time Password (TOTP)</p>
          </div>
        </div>

        {errorMsg && (
          <div className="mb-4 p-3 bg-rose-500/10 border border-rose-500/30 rounded-xl text-rose-400 text-xs flex items-center gap-2">
            <AlertCircle className="w-4 h-4 shrink-0" />
            <span>{errorMsg}</span>
          </div>
        )}

        {successMsg && (
          <div className="mb-4 p-3 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-emerald-400 text-xs flex items-center gap-2">
            <Check className="w-4 h-4 shrink-0" />
            <span>{successMsg}</span>
          </div>
        )}

        {user?.isTwoFactorEnabled ? (
          <div className="py-4 text-center">
            <div className="p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-2xl mb-6 inline-block">
              <ShieldCheck className="w-12 h-12 text-emerald-400 mx-auto" />
            </div>
            <h3 className="text-lg font-semibold text-white mb-2">2FA is Currently Active</h3>
            <p className="text-sm text-slate-400 max-w-sm mx-auto mb-6">
              Your account is secured with Two-Factor Authentication. A 6-digit code from your authenticator app is required at each login.
            </p>
            <button
              onClick={handleDisable2FA}
              disabled={isLoading}
              className="px-6 py-2.5 bg-rose-600/20 border border-rose-500/40 hover:bg-rose-600 text-rose-300 hover:text-white font-medium rounded-xl transition-colors"
            >
              {isLoading ? 'Processing...' : 'Disable Two-Factor Authentication'}
            </button>
          </div>
        ) : setupData ? (
          <div className="space-y-4">
            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 flex flex-col sm:flex-row items-center gap-4">
              <div className="bg-white p-2 rounded-lg shrink-0">
                <QRCodeSVG value={setupData.qrCodeUri} size={130} />
              </div>
              <div className="space-y-2 text-left">
                <p className="text-xs text-slate-300 font-medium">1. Scan QR Code</p>
                <p className="text-xs text-slate-400">
                  Open Google Authenticator, Microsoft Authenticator, 1Password, or Authy and scan this QR code.
                </p>
                <div className="pt-1">
                  <p className="text-[11px] text-slate-500 uppercase tracking-wider mb-1">Manual Setup Key</p>
                  <div className="flex items-center gap-2 bg-slate-900 px-2.5 py-1.5 rounded-lg border border-slate-700">
                    <span className="font-mono text-xs text-emerald-400 select-all">{setupData.secretKey}</span>
                    <button onClick={handleCopy} className="text-slate-400 hover:text-white">
                      {copied ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                    </button>
                  </div>
                </div>
              </div>
            </div>

            <form onSubmit={handleVerifyAndEnable} className="space-y-3">
              <div>
                <label className="block text-xs font-semibold text-slate-300 uppercase tracking-wider mb-1.5">
                  2. Enter 6-Digit Verification Code
                </label>
                <div className="relative">
                  <KeyRound className="w-4 h-4 absolute left-3 top-1/2 -translate-y-1/2 text-slate-500" />
                  <input
                    type="text"
                    maxLength={6}
                    placeholder="000000"
                    value={code}
                    onChange={(e) => setCode(e.target.value.replace(/\D/g, ''))}
                    className="w-full bg-slate-950 border border-slate-700 rounded-xl py-2.5 pl-10 pr-4 text-center text-xl tracking-[0.3em] font-mono text-emerald-400 focus:outline-none focus:border-emerald-500"
                  />
                </div>
              </div>

              <button
                type="submit"
                disabled={code.length !== 6 || isLoading}
                className="w-full py-3 bg-emerald-600 hover:bg-emerald-500 disabled:opacity-50 text-white font-semibold rounded-xl transition-colors shadow-lg shadow-emerald-950 flex items-center justify-center gap-2"
              >
                {isLoading ? 'Activating...' : 'Verify Code & Enable 2FA'}
              </button>
            </form>
          </div>
        ) : (
          <div className="py-8 text-center text-slate-400">Loading 2FA Setup...</div>
        )}
      </div>
    </div>
  );
};
