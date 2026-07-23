'use client';

// useSessionTimeout — DL 71: Session timeout warning UI
//
// Parses the JWT expiry (`exp` claim) and warns the user 1 minute before it expires.
// Two actions: "Stay logged in" (refresh token) or "Logout" (clear + redirect).

import { useEffect, useState, useCallback, useRef } from 'react';
import { useRouter } from 'next/navigation';

const WARNING_BEFORE_SECONDS = 60; // warn 1 minute before expiry
const CHECK_INTERVAL_MS = 5000; // check every 5 seconds

interface SessionState {
  showWarning: boolean;
  secondsRemaining: number;
  refresh: () => Promise<void>;
  logout: () => void;
}

function parseJwt(token: string): { exp?: number } | null {
  try {
    const part = token.split('.')[1];
    if (!part) return null;
    const decoded = atob(part.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(decoded);
  } catch {
    return null;
  }
}

export function useSessionTimeout(): SessionState {
  const router = useRouter();
  const [showWarning, setShowWarning] = useState(false);
  const [secondsRemaining, setSecondsRemaining] = useState(0);
  const intervalRef = useRef<NodeJS.Timeout | null>(null);
  const warnedRef = useRef(false);

  const refresh = useCallback(async () => {
    try {
      const refreshToken = typeof window !== 'undefined' ? localStorage.getItem('refreshToken') : null;
      if (!refreshToken) {
        router.push('/login');
        return;
      }
      const res = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken }),
      });
      if (!res.ok) {
        router.push('/login');
        return;
      }
      const data = await res.json();
      localStorage.setItem('accessToken', data.accessToken);
      if (data.refreshToken) localStorage.setItem('refreshToken', data.refreshToken);
      if (data.user) localStorage.setItem('user', JSON.stringify(data.user));
      setShowWarning(false);
      warnedRef.current = false;
    } catch {
      router.push('/login');
    }
  }, [router]);

  const logout = useCallback(() => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refreshToken');
    localStorage.removeItem('user');
    router.push('/login');
  }, [router]);

  useEffect(() => {
    const tick = () => {
      const token = typeof window !== 'undefined' ? localStorage.getItem('accessToken') : null;
      if (!token) {
        setShowWarning(false);
        return;
      }
      const payload = parseJwt(token);
      if (!payload?.exp) {
        setShowWarning(false);
        return;
      }
      const now = Math.floor(Date.now() / 1000);
      const remaining = payload.exp - now;
      setSecondsRemaining(Math.max(0, remaining));

      if (remaining <= 0) {
        // Expired
        logout();
      } else if (remaining <= WARNING_BEFORE_SECONDS && !warnedRef.current) {
        setShowWarning(true);
        warnedRef.current = true;
      } else if (remaining > WARNING_BEFORE_SECONDS) {
        // Reset warning state if user refreshed
        setShowWarning(false);
        warnedRef.current = false;
      }
    };

    tick();
    intervalRef.current = setInterval(tick, CHECK_INTERVAL_MS);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, [logout]);

  return { showWarning, secondsRemaining, refresh, logout };
}