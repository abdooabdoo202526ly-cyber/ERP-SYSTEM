'use client';

// إنشاء حساب جديد (Account)

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { financeApi, getErrorMessage, Account } from '@/lib/api';

const ACCOUNT_TYPES = [
  { label: 'أصول (Asset)', value: 1 },
  { label: 'خصوم (Liability)', value: 2 },
  { label: 'حقوق ملكية (Equity)', value: 3 },
  { label: 'إيرادات (Revenue)', value: 4 },
  { label: 'مصروفات (Expense)', value: 5 },
];

const NORMAL_BALANCE = [
  { label: 'مدين (Debit)', value: 1 },
  { label: 'دائن (Credit)', value: 2 },
];

interface FormState {
  code: string;
  name: string;
  description: string;
  type: number;
  normalBalance: number;
  parentAccountId: string;
  isPostable: boolean;
}

export default function NewAccountPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    code: '',
    name: '',
    description: '',
    type: 1,
    normalBalance: 1,
    parentAccountId: '',
    isPostable: true,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: ['type', 'normalBalance'].includes(k as string) ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      // v1.0.21: use financeApi (axios) so the response interceptor
      // can surface validation errors in a readable form.
      await financeApi.createAccount({
        code: form.code,
        name: form.name,
        description: form.description || undefined,
        type: form.type,
        normalBalance: form.normalBalance,
        parentAccountId: form.parentAccountId || undefined,
        isPostable: form.isPostable,
      } as Partial<Account>);
      router.push('/finance/accounts');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الحساب.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ حساب جديد"
        description="أضف حساباً جديداً إلى دليل الحسابات"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'دليل الحسابات', href: '/finance/accounts' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/accounts">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="كود الحساب *" value={form.code} onChange={onChange('code')} required placeholder="مثال: 1110" />
            <Select label="نوع الحساب *" value={String(form.type)} onChange={onChange('type')} options={ACCOUNT_TYPES} />
          </div>

          <Input label="اسم الحساب *" value={form.name} onChange={onChange('name')} required placeholder="مثال: النقدية بالصندوق" />

          <Input label="الوصف" value={form.description} onChange={onChange('description')} placeholder="اختياري" />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select label="الرصيد الطبيعي *" value={String(form.normalBalance)} onChange={onChange('normalBalance')} options={NORMAL_BALANCE} />
            <Input label="الحساب الأب (اختياري)" value={form.parentAccountId} onChange={onChange('parentAccountId')} placeholder="UUID" />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.isPostable}
              onChange={(e) => setForm({ ...form, isPostable: e.target.checked })}
              className="rounded border-gray-300"
            />
            <span>قابل للترحيل (Postable)</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
              حفظ
            </Button>
            <Link href="/finance/accounts">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
