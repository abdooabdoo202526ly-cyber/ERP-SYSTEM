'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { authApi } from '@/lib/api';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await authApi.login({ email, password });
      router.push('/dashboard');
    } catch (err: any) {
      setError(err?.response?.data?.detail || 'فشل تسجيل الدخول - تحقق من البيانات');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 p-6" dir="rtl">
      <div className="bg-white rounded-2xl shadow-xl p-8 w-full max-w-md">
        <h1 className="text-3xl font-bold text-gray-800 mb-2">🔐 تسجيل الدخول</h1>
        <p className="text-gray-500 mb-6">ERP-SYSTEM</p>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">البريد الإلكتروني</label>
            <input
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              required
              data-testid="email"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور</label>
            <input
              type="password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              className="w-full border border-gray-300 rounded-lg px-4 py-2 focus:ring-2 focus:ring-blue-500 focus:border-transparent"
              required
              data-testid="password"
            />
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-blue-600 text-white rounded-lg px-4 py-2.5 font-semibold hover:bg-blue-700 disabled:opacity-50"
          >
            {loading ? 'جاري الدخول...' : 'دخول'}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-gray-600">
          ليس لديك حساب؟{' '}
          <Link href="/register" className="text-blue-600 font-semibold hover:underline">
            إنشاء حساب جديد
          </Link>
        </p>
        <p className="mt-2 text-center text-sm">
          <Link href="/login/forgot" className="text-blue-600 hover:underline">
            نسيت كلمة المرور؟
          </Link>
        </p>
        <p className="mt-2 text-center text-sm">
          <Link href="/" className="text-gray-500 hover:underline">
            ← العودة للرئيسية
          </Link>
        </p>
      </div>
    </main>
  );
}
