'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { authApi } from '@/lib/api';

export default function RegisterPage() {
  const router = useRouter();
  // ملاحظة: لا نطلب "subdomain" — الـ backend يحسبه تلقائياً من TenantName عبر Slugify
  const [form, setForm] = useState({
    email: '',
    password: '',
    fullName: '',
    tenantName: '',
  });
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onChange = (k: keyof typeof form) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setForm({ ...form, [k]: e.target.value });
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      await authApi.register(form);
      router.push('/dashboard');
    } catch (err: any) {
      setError(err?.response?.data?.detail || 'فشل التسجيل - تحقق من البيانات');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 p-6" dir="rtl">
      <div className="bg-white rounded-2xl shadow-xl p-8 w-full max-w-md">
        <h1 className="text-3xl font-bold text-gray-800 mb-2">📝 إنشاء حساب</h1>
        <p className="text-gray-500 mb-6">سجّل شركتك وابدأ باستخدام ERP-SYSTEM</p>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-3">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">الاسم الكامل</label>
            <input
              type="text"
              value={form.fullName}
              onChange={onChange('fullName')}
              className="w-full border border-gray-300 rounded-lg px-4 py-2"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">البريد الإلكتروني</label>
            <input
              type="email"
              value={form.email}
              onChange={onChange('email')}
              className="w-full border border-gray-300 rounded-lg px-4 py-2"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">كلمة المرور (8+ حروف، أحرف كبيرة وصغيرة ورموز)</label>
            <input
              type="password"
              value={form.password}
              onChange={onChange('password')}
              className="w-full border border-gray-300 rounded-lg px-4 py-2"
              required
              minLength={8}
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">اسم الشركة</label>
            <input
              type="text"
              value={form.tenantName}
              onChange={onChange('tenantName')}
              className="w-full border border-gray-300 rounded-lg px-4 py-2"
              required
            />
            <p className="text-xs text-gray-500 mt-1">
              سيُولَّد subdomain شركتك تلقائياً من اسمها (مثال: &quot;شركة الأمل&quot; → subdomain: <code>shrkt-lamal</code>)
            </p>
          </div>
          <button
            type="submit"
            disabled={loading}
            className="w-full bg-green-600 text-white rounded-lg px-4 py-2.5 font-semibold hover:bg-green-700 disabled:opacity-50 mt-4"
          >
            {loading ? 'جاري التسجيل...' : 'تسجيل'}
          </button>
        </form>

        <p className="mt-6 text-center text-sm text-gray-600">
          لديك حساب؟{' '}
          <Link href="/login" className="text-blue-600 font-semibold hover:underline">
            دخول
          </Link>
        </p>
      </div>
    </main>
  );
}
