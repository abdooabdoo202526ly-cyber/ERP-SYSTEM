'use client';

// إنشاء مركز تكلفة جديد (Cost Center)

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

const CC_TYPES = [
  { label: 'إنتاج (Production)', value: 1 },
  { label: 'خدمات (Service)', value: 2 },
  { label: 'إداري (Administrative)', value: 3 },
  { label: 'مبيعات (Sales)', value: 4 },
];

interface FormState {
  code: string;
  name: string;
  description: string;
  type: number;
  parentId: string;
}

export default function NewCostCenterPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    code: '',
    name: '',
    description: '',
    type: 3,
    parentId: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'type' ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/api/cost-centers', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...form,
          parentId: form.parentId || null,
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء مركز التكلفة');
      }
      router.push('/finance/cost-centers');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء مركز التكلفة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ مركز تكلفة جديد"
        description="أضف مركز تكلفة جديداً"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'مراكز التكلفة', href: '/finance/cost-centers' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/cost-centers">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="الكود *" value={form.code} onChange={onChange('code')} required placeholder="مثال: CC-001" />
            <Select label="النوع" value={String(form.type)} onChange={onChange('type')} options={CC_TYPES} />
          </div>

          <Input label="الاسم *" value={form.name} onChange={onChange('name')} required placeholder="مثال: قسم المشتريات" />

          <Input label="الوصف" value={form.description} onChange={onChange('description')} placeholder="اختياري" />

          <Input label="المركز الأب (Parent ID)" value={form.parentId} onChange={onChange('parentId')} placeholder="UUID أو اتركه فارغاً" />

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
              حفظ
            </Button>
            <Link href="/finance/cost-centers">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
