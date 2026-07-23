'use client';

// صفحة إنشاء دورة رواتب جديدة — form: period_start + period_end + notes

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { hrApi, CreatePayrollRunRequest, getErrorMessage } from '@/lib/api';

interface FormState {
  periodStart: string;
  periodEnd: string;
  notes: string;
}

export default function NewPayrollRunPage() {
  const router = useRouter();
  const [form, setForm] = useState<FormState>({
    periodStart: '',
    periodEnd: '',
    notes: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = (k: keyof FormState) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>
  ) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!form.periodStart || !form.periodEnd) {
      setError('تاريخ البداية والنهاية مطلوبان.');
      return;
    }
    if (new Date(form.periodEnd) < new Date(form.periodStart)) {
      setError('تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.');
      return;
    }

    setSubmitting(true);
    try {
      const payload: CreatePayrollRunRequest = {
        periodStart: form.periodStart,
        periodEnd: form.periodEnd,
        notes: form.notes || undefined,
      };
      const created = await hrApi.payroll.createPayrollRun(payload);
      router.push(`/hr/payroll/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الدورة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ دورة رواتب جديدة"
        description="أنشئ دورة جديدة بحالة Draft — ثم عالجها لاحقاً"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'Payroll', href: '/hr/payroll' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/hr/payroll">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
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
            <Input
              type="date"
              label="تاريخ البداية *"
              value={form.periodStart}
              onChange={onChange('periodStart')}
              required
            />
            <Input
              type="date"
              label="تاريخ النهاية *"
              value={form.periodEnd}
              onChange={onChange('periodEnd')}
              required
            />
          </div>

          <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-3 text-xs text-blue-800">
            <p className="font-semibold mb-1">ℹ️ ملاحظة:</p>
            <ul className="list-disc list-inside space-y-0.5">
              <li>الدورة تُنشأ بحالة <strong>Draft</strong> فور الإنشاء.</li>
              <li>تستطيع إنشاء دورة واحدة فقط لكل فترة (لا تداخل).</li>
              <li>بعد الإنشاء، اذهب إلى صفحة التفاصيل واضغط <strong>معالجة</strong> لحساب payslips.</li>
            </ul>
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظات</label>
            <textarea
              value={form.notes}
              onChange={onChange('notes')}
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
              placeholder="مثال: رواتب شهر يونيو 2026"
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              إنشاء الدورة
            </Button>
            <Link href="/hr/payroll">
              <Button type="button" variant="ghost">
                إلغاء
              </Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}