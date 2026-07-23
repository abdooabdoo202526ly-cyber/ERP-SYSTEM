'use client';

// الصفحة الرئيسية — redirect إلى /login أو /dashboard بحسب الـ auth state

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { authApi } from '@/lib/api';

export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    if (authApi.isLoggedIn()) {
      router.push('/dashboard');
    } else {
      router.push('/login');
    }
  }, [router]);

  return (
    <main className="min-h-screen flex items-center justify-center" dir="rtl">
      <div className="text-center">
        <div className="inline-block h-12 w-12 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
        <p className="mt-4 text-gray-500 text-sm">جاري التحويل...</p>
      </div>
    </main>
  );
}
