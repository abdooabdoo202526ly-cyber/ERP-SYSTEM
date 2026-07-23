'use client';

// تعديل قاعدة ترحيل (Posting Rule)

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

const EVENT_TYPES = [
  { label: 'استلام مخزون (StockReceived)', value: 1 },
  { label: 'صرف مخزون (StockIssued)', value: 2 },
  { label: 'إنشاء فاتورة (InvoiceCreated)', value: 3 },
  { label: 'استلام دفعة (PaymentReceived)', value: 4 },
];

interface FormState {
  name: string;
  description: string;
  eventType: number;
  templateJson: string;
  isActive: boolean;
}

export default function EditPostingRulePage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const id = params?.id;
  useAuth();

  const [form, setForm] = useState<FormState>({
    name: '',
    description: '',
    eventType: 1,
    templateJson: '{}',
    isActive: true,
  });
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    fetch(`/api/finance/posting-rules/${id}`)
      .then(async (r) => {
        if (!r.ok) throw new Error('فشل التحميل');
        return r.json();
      })
      .then((r: { name: string; description?: string; eventType: number; templateJson: string; isActive: boolean }) => {
        setForm({
          name: r.name || '',
          description: r.description || '',
          eventType: r.eventType || 1,
          templateJson: r.templateJson || '{}',
          isActive: r.isActive ?? true,
        });
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل التحميل.')))
      .finally(() => setLoading(false));
  }, [id]);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'eventType' ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    try {
      JSON.parse(form.templateJson);
    } catch {
      setError('قالب JSON غير صالح.');
      return;
    }
    setSubmitting(true);
    try {
      const res = await fetch(`/api/finance/posting-rules/${id}`, {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: form.name,
          description: form.description || null,
          eventType: form.eventType,
          isActive: form.isActive,
          templateJson: form.templateJson,
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل تحديث القاعدة');
      }
      router.push(`/admin/posting-rules/${id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث القاعدة.'));
      setSubmitting(false);
    }
  };

  if (loading) return <div className="p-8 text-center text-gray-500">جاري التحميل…</div>;

  return (
    <div>
      <PageHeader
        title="✏️ تعديل قاعدة ترحيل"
        description="عدّل القالب والوصف والحالة"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'قواعد الترحيل', href: '/admin/posting-rules' },
          { label: form.name || 'تعديل' },
        ]}
        actions={
          <Link href={`/admin/posting-rules/${id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4 max-w-3xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error}</div>
        )}

        <Card title="📋 معلومات أساسية">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="اسم القاعدة *"
              value={form.name}
              onChange={onChange('name')}
              required
            />
            <Select
              label="نوع الحدث *"
              value={form.eventType}
              onChange={onChange('eventType')}
              options={EVENT_TYPES}
            />
            <Input
              label="الوصف"
              value={form.description}
              onChange={onChange('description')}
            />
            <label className="flex items-center gap-2 mt-6 text-sm">
              <input
                type="checkbox"
                checked={form.isActive}
                onChange={(e) => setForm((f) => ({ ...f, isActive: e.target.checked }))}
                className="h-4 w-4 rounded border-gray-300 text-blue-600"
              />
              نشطة
            </label>
          </div>
        </Card>

        <Card title="🧩 قالب JSON">
          <textarea
            value={form.templateJson}
            onChange={onChange('templateJson')}
            rows={14}
            className="w-full font-mono text-xs rounded-lg border border-gray-300 bg-gray-50 px-3 py-2 focus:border-blue-500 focus:outline-none"
            placeholder='{"description": "...", "lines": [...]}'
          />
          <p className="text-xs text-gray-500 mt-2">قالب JSON يحدد الـ JournalEntry الذي سيُنشأ.</p>
        </Card>

        <div className="flex items-center gap-2 pt-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
          >
            حفظ التغييرات
          </Button>
          <Link href={`/admin/posting-rules/${id}`}>
            <Button type="button" variant="ghost">إلغاء</Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
