'use client';

// Hook موحد للتحقق من الـ authentication
// يعيد redirect إلى /login إن لم يكن المستخدم مسجلاً دخوله

import { useEffect, useRef, useState } from 'react';
import { useRouter } from 'next/navigation';
import { authApi, UserInfo } from '@/lib/api';

export interface UseAuthResult {
  /** الـ user الحالي (null أثناء التحميل أو غير مسجل) */
  user: UserInfo | null;
  /** هل الصفحة لا تزال تتحقق من الـ session؟ */
  loading: boolean;
}

export function useAuth(): UseAuthResult {
  const router = useRouter();
  const [user, setUser] = useState<UserInfo | null>(null);
  const [loading, setLoading] = useState(true);
  // DEC-040: Use ref to prevent the auth check from running on every render.
  // The router object from useRouter() is a new instance on every render, which
  // caused an infinite re-render loop (React error #185). The ref guard runs
  // the auth check exactly once per component mount.
  const didCheckRef = useRef(false);

  useEffect(() => {
    if (didCheckRef.current) return;
    didCheckRef.current = true;

    if (!authApi.isLoggedIn()) {
      router.push('/login');
      setLoading(false);
      return;
    }
    setUser(authApi.getUser());
    setLoading(false);
  }, [router]);

  return { user, loading };
}