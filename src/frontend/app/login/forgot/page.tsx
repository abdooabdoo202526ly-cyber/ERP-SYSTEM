'use client';

// صفحة طلب إعادة تعيين كلمة المرور (Forgot Password)

import { useState } from 'react';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState('');
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [devInfo, setDevInfo] = useState<{ token: string; resetUrl: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setMessage(null);
    setDevInfo(null);
    setLoading(true);
    try {
      const res = await fetch('/api/auth/forgot-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.detail || 'فشل إنشاء رمز إعادة التعيين.');
        return;
      }
      setMessage(data.message);
      // في الـ demo نُظهر الـ token والرابط
      if (data.devToken && data.resetUrl) {
        setDevInfo({ token: data.devToken, resetUrl: data.resetUrl });
      }
    } catch (e) {
      setError('فشل الاتصال بالخادم.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 p-6" dir="rtl">
      <Card className="w-full max-w-md p-8">
        <h1 className="text-2xl font-bold text-gray-800 mb-2">🔑 نسيت كلمة المرور؟</h1>
        <p className="text-gray-500 mb-6 text-sm">
          أدخل بريدك الإلكتروني وسنرسل لك رمزاً لإعادة التعيين.
        </p>

        {message && (
          <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {message}
          </div>
        )}

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        {devInfo && (
          <div className="bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-3 rounded-lg mb-4 text-xs">
            <p className="font-bold mb-1">🛠 وضع التطوير (Demo Mode):</p>
            <p className="font-mono break-all">الرمز: {devInfo.token}</p>
            <p className="mt-2">
              <Link href={devInfo.resetUrl} className="text-blue-600 font-bold underline">
                اضغط هنا لإعادة التعيين
              </Link>
            </p>
            <p className="text-gray-600 mt-1">(في الإنتاج يُرسل هذا الرابط بالبريد الإلكتروني)</p>
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <Input
            label="البريد الإلكتروني"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            placeholder="user@example.com"
          />
          <Button type="submit" variant="primary" loading={loading} className="w-full">
            إرسال رمز إعادة التعيين
          </Button>
        </form>

        <div className="mt-6 flex items-center justify-between text-sm">
          <Link href="/login" className="text-blue-600 hover:underline flex items-center gap-1">
            <ArrowRight className="h-4 w-4" /> العودة لتسجيل الدخول
          </Link>
        </div>
      </Card>
    </main>
  );
}