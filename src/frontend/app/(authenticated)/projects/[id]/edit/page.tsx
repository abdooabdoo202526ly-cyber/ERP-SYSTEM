'use client';

// صفحة تعديل المشروع (Project) — form

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

const PROJECT_STATUSES = [
  { label: 'تخطيط (Planning)', value: 1 },
  { label: 'نشط (Active)', value: 2 },
  { label: 'معلّق (OnHold)', value: 3 },
  { label: 'مكتمل (Completed)', value: 4 },
  { label: 'ملغي (Cancelled)', value: 5 },
];

interface FormState {
  code: string;
  name: string;
  description: string;
  costCenterId: string;
  status: number;
  budget: string;
  startDate: string;
  endDate: string;
  isActive: boolean;
}

export default function EditProjectPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [form, setForm] = useState<FormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch(`/api/projects/${params.id}`);
        if (!res.ok) throw new Error('فشل تحميل المشروع');
        const data = await res.json();
        setForm({
          code: data.code || '',
          name: data.name || '',
          description: data.description || '',
          costCenterId: data.costCenterId || '',
          status: data.status ?? 2,
          budget: String(data.budget ?? 0),
          startDate: data.startDate ? data.startDate.split('T')[0] : '',
          endDate: data.endDate ? data.endDate.split('T')[0] : '',
          isActive: data.isActive ?? true,
        });
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل تحميل المشروع'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    if (!form) return;
    const v = e.target.value;
    setForm({ ...form, [k]: k === 'status' ? Number(v) : v });
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch(`/api/projects/${params.id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...form,
          budget: Number(form.budget),
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل تحديث المشروع');
      }
      router.push('/projects');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث المشروع.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="✏️ تعديل مشروع" />
        <Card className="max-w-2xl">
          <div className="text-center py-12 text-gray-500">جاري التحميل...</div>
        </Card>
      </div>
    );
  }

  if (!form) {
    return (
      <div>
        <PageHeader title="✏️ تعديل مشروع" />
        <Card className="max-w-2xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'المشروع غير موجود'}
          </div>
          <div className="mt-4">
            <Link href="/projects">
              <Button variant="ghost">رجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل مشروع"
        description={form.name}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المشاريع', href: '/projects' },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href="/projects">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للقائمة
            </Button>
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
            <Input label="كود المشروع *" value={form.code} onChange={onChange('code')} required />
            <Select label="الحالة" value={String(form.status)} onChange={onChange('status')} options={PROJECT_STATUSES} />
          </div>

          <Input label="اسم المشروع *" value={form.name} onChange={onChange('name')} required />

          <Input label="الوصف" value={form.description} onChange={onChange('description')} />

          <Input label="مركز التكلفة *" value={form.costCenterId} onChange={onChange('costCenterId')} required />

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input label="الميزانية" type="number" step="0.01" value={form.budget} onChange={onChange('budget')} />
            <Input label="تاريخ البداية *" type="date" value={form.startDate} onChange={onChange('startDate')} required />
            <Input label="تاريخ النهاية" type="date" value={form.endDate} onChange={onChange('endDate')} />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              className="rounded border-gray-300"
            />
            <span>فعّال</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
              حفظ التعديلات
            </Button>
            <Link href="/projects">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
