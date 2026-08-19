import React, { createContext, useContext, useState, useEffect } from 'react';
import { UserProfile, AuthResponse } from '../types';
import { AuthApi } from '../services/api';

interface AuthContextType {
  user: UserProfile | null;
  token: string | null;
  isLoading: boolean;
  loginModalOpen: boolean;
  twoFactorSetupOpen: boolean;
  requiresTwoFactorChallenge: boolean;
  challengeToken: string | null;
  openLoginModal: () => void;
  closeLoginModal: () => void;
  openTwoFactorSetup: () => void;
  closeTwoFactorSetup: () => void;
  loginWithGoogle: (credential: string) => Promise<AuthResponse>;
  verifyTwoFactor: (code: string) => Promise<boolean>;
  logout: () => void;
  refreshUser: () => Promise<void>;
}

const AuthContext = createContext<AuthContextType | undefined>(undefined);

export const AuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
  const [user, setUser] = useState<UserProfile | null>(null);
  const [token, setToken] = useState<string | null>(() => localStorage.getItem('mtt_jwt_token'));
  const [isLoading, setIsLoading] = useState<boolean>(true);
  const [loginModalOpen, setLoginModalOpen] = useState<boolean>(false);
  const [twoFactorSetupOpen, setTwoFactorSetupOpen] = useState<boolean>(false);
  const [requiresTwoFactorChallenge, setRequiresTwoFactorChallenge] = useState<boolean>(false);
  const [challengeToken, setChallengeToken] = useState<string | null>(null);

  const refreshUser = async () => {
    const storedToken = localStorage.getItem('mtt_jwt_token');
    if (!storedToken) {
      setUser(null);
      setIsLoading(false);
      return;
    }

    try {
      const profile = await AuthApi.getCurrentUser();
      setUser(profile);
    } catch {
      // Token expired or invalid
      localStorage.removeItem('mtt_jwt_token');
      setToken(null);
      setUser(null);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    refreshUser();
  }, []);

  const loginWithGoogle = async (credential: string): Promise<AuthResponse> => {
    try {
      const res = await AuthApi.loginWithGoogle(credential);
      if (res.requiresTwoFactor && res.twoFactorChallengeToken) {
        setRequiresTwoFactorChallenge(true);
        setChallengeToken(res.twoFactorChallengeToken);
        setLoginModalOpen(true);
        return res;
      }

      if (res.token && res.user) {
        localStorage.setItem('mtt_jwt_token', res.token);
        setToken(res.token);
        setUser(res.user);
        setRequiresTwoFactorChallenge(false);
        setChallengeToken(null);
        setLoginModalOpen(false);
      }
      return res;
    } catch (err: any) {
      return {
        success: false,
        requiresTwoFactor: false,
        message: err?.response?.data?.message || 'Google authentication failed.'
      };
    }
  };

  const verifyTwoFactor = async (code: string): Promise<boolean> => {
    try {
      const res = await AuthApi.verifyTwoFactor({
        twoFactorChallengeToken: challengeToken || undefined,
        code
      });

      if (res.token && res.user) {
        localStorage.setItem('mtt_jwt_token', res.token);
        setToken(res.token);
        setUser(res.user);
        setRequiresTwoFactorChallenge(false);
        setChallengeToken(null);
        setLoginModalOpen(false);
        return true;
      }
      return false;
    } catch {
      return false;
    }
  };

  const logout = () => {
    localStorage.removeItem('mtt_jwt_token');
    setToken(null);
    setUser(null);
    setRequiresTwoFactorChallenge(false);
    setChallengeToken(null);
  };

  return (
    <AuthContext.Provider
      value={{
        user,
        token,
        isLoading,
        loginModalOpen,
        twoFactorSetupOpen,
        requiresTwoFactorChallenge,
        challengeToken,
        openLoginModal: () => setLoginModalOpen(true),
        closeLoginModal: () => {
          setLoginModalOpen(false);
          setRequiresTwoFactorChallenge(false);
          setChallengeToken(null);
        },
        openTwoFactorSetup: () => setTwoFactorSetupOpen(true),
        closeTwoFactorSetup: () => setTwoFactorSetupOpen(false),
        loginWithGoogle,
        verifyTwoFactor,
        logout,
        refreshUser
      }}
    >
      {children}
    </AuthContext.Provider>
  );
};

export const useAuth = () => {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
};
