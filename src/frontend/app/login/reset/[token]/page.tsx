'use client';

// صفحة إعادة تعيين كلمة المرور (Reset Password)

import { useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Lock } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';

export default function ResetPasswordPage() {
  const params = useParams<{ token: string }>();
  const router = useRouter();
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (newPassword.length < 8) {
      setError('كلمة المرور يجب أن تكون 8 أحرف على الأقل.');
      return;
    }
    if (newPassword !== confirmPassword) {
      setError('كلمتا المرور غير متطابقتين.');
      return;
    }

    setLoading(true);
    try {
      const res = await fetch('/api/auth/reset-password', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ token: params.token, newPassword }),
      });
      const data = await res.json();
      if (!res.ok) {
        setError(data.detail || 'فشل إعادة تعيين كلمة المرور.');
        return;
      }
      setSuccess(true);
      setTimeout(() => router.push('/login'), 2000);
    } catch (e) {
      setError('فشل الاتصال بالخادم.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <main className="min-h-screen flex items-center justify-center bg-gradient-to-br from-blue-50 to-indigo-100 p-6" dir="rtl">
      <Card className="w-full max-w-md p-8">
        <h1 className="text-2xl font-bold text-gray-800 mb-2">
          <Lock className="inline h-6 w-6 ml-2" />
          إعادة تعيين كلمة المرور
        </h1>
        <p className="text-gray-500 mb-6 text-sm">أدخل كلمة المرور الجديدة</p>

        {success ? (
          <div className="bg-green-50 border border-green-200 text-green-700 px-4 py-3 rounded-lg mb-4 text-sm">
            ✅ تم تحديث كلمة المرور بنجاح. سيتم تحويلك لصفحة الدخول...
          </div>
        ) : (
          <>
            {error && (
              <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
                {error}
              </div>
            )}

            <form onSubmit={onSubmit} className="space-y-4">
              <Input
                label="كلمة المرور الجديدة *"
                type="password"
                value={newPassword}
                onChange={(e) => setNewPassword(e.target.value)}
                required
                placeholder="8 أحرف على الأقل"
              />
              <Input
                label="تأكيد كلمة المرور *"
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                required
                placeholder="أعد إدخال كلمة المرور"
              />
              <Button type="submit" variant="primary" loading={loading} className="w-full">
                تحديث كلمة المرور
              </Button>
            </form>
          </>
        )}

        <div className="mt-6 flex items-center justify-between text-sm">
          <Link href="/login" className="text-blue-600 hover:underline flex items-center gap-1">
            <ArrowRight className="h-4 w-4" /> العودة لتسجيل الدخول
          </Link>
        </div>
      </Card>
    </main>
  );
}