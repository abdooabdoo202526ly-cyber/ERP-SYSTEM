'use client';

// إنشاء قاعدة ترحيل جديدة (Posting Rule)

import { useState } from 'react';
import { useRouter } from 'next/navigation';
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

const DEFAULT_TEMPLATE = JSON.stringify({
  description: 'ترحيل تلقائي',
  reference: 'AUTO-{reference}',
  lines: [
    { accountCode: '1110', side: 'debit', amountFormula: '{amount}' },
    { accountCode: '2010', side: 'credit', amountFormula: '{amount}' },
  ],
}, null, 2);

interface FormState {
  name: string;
  description: string;
  eventType: number;
  templateJson: string;
}

export default function NewPostingRulePage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    name: '',
    description: '',
    eventType: 1,
    templateJson: DEFAULT_TEMPLATE,
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'eventType' ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    // Validate JSON
    try {
      JSON.parse(form.templateJson);
    } catch {
      setError('قالب JSON غير صالح.');
      return;
    }
    setSubmitting(true);
    try {
      const res = await fetch('/api/finance/posting-rules', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          name: form.name,
          description: form.description || null,
          eventType: form.eventType,
          isActive: true,
          templateJson: form.templateJson,
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء القاعدة');
      }
      router.push('/admin/posting-rules');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء القاعدة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ قاعدة ترحيل جديدة"
        description="حدد قاعدة ترحيل تلقائي لحدث معين"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'قواعد الترحيل', href: '/admin/posting-rules' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/admin/posting-rules">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-3xl">
        {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="اسم القاعدة *" value={form.name} onChange={onChange('name')} required placeholder="مثال: ترحيل استلام المخزون" />
            <Select label="نوع الحدث *" value={String(form.eventType)} onChange={onChange('eventType')} options={EVENT_TYPES} />
          </div>

          <Input label="الوصف" value={form.description} onChange={onChange('description')} placeholder="اختياري" />

          <div>
            <label className="block text-sm text-gray-500 mb-1">قالب JSON *</label>
            <textarea
              value={form.templateJson}
              onChange={onChange('templateJson')}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm font-mono"
              rows={12}
              required
            />
            <p className="text-xs text-gray-500 mt-1">
              💡 المتغيرات المتاحة: {'{amount}'}, {'{reference}'}. الحسابات تُحدّد بكودها (AccountCode).
            </p>
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>حفظ</Button>
            <Link href="/admin/posting-rules">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
