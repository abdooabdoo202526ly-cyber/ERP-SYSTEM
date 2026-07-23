'use client';

// صفحة إنشاء مشروع جديد (Project) — form

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { projectsApi, getErrorMessage, Project } from '@/lib/api';

interface CostCenterLite {
  id: string;
  code: string;
  name: string;
}

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
}

export default function NewProjectPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    code: '',
    name: '',
    description: '',
    costCenterId: '',
    status: 2,
    budget: '0',
    startDate: new Date().toISOString().split('T')[0],
    endDate: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [costCenters, setCostCenters] = useState<CostCenterLite[]>([]);

  // v1.0.22: load cost centers for the dropdown
  useEffect(() => {
    projectsApi.listCostCenters().then(setCostCenters).catch(() => setCostCenters([]));
  }, []);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'status' ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      // v1.0.22: use projectsApi (axios) so 400 errors show details
      await projectsApi.createProject({
        code: form.code,
        name: form.name,
        description: form.description || undefined,
        costCenterId: form.costCenterId || undefined,
        status: form.status,
        budget: Number(form.budget) || 0,
        startDate: form.startDate,
        endDate: form.endDate || undefined,
      } as Partial<Project>);
      router.push('/projects');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء المشروع. تأكد من البيانات وأن الـ backend يدعم الـ endpoint.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ مشروع جديد"
        description="أضف مشروعاً جديداً إلى النظام"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المشاريع', href: '/projects' },
          { label: 'جديد' },
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
            <Input
              label="كود المشروع *"
              value={form.code}
              onChange={onChange('code')}
              required
              placeholder="مثال: PRJ-001"
            />
            <Select
              label="الحالة"
              value={String(form.status)}
              onChange={onChange('status')}
              options={PROJECT_STATUSES}
            />
          </div>

          <Input
            label="اسم المشروع *"
            value={form.name}
            onChange={onChange('name')}
            required
            placeholder="مثال: بناء مجمع سكني - طرابلس"
          />

          <Input
            label="الوصف"
            value={form.description}
            onChange={onChange('description')}
            placeholder="وصف تفصيلي للمشروع (اختياري)"
          />

          <Select
            label="مركز التكلفة *"
            value={form.costCenterId}
            onChange={onChange('costCenterId')}
            options={[
              { value: '', label: costCenters.length === 0 ? 'لا توجد مراكز تكلفة — أنشئ واحداً أولاً' : 'اختر مركز التكلفة' },
              ...costCenters.map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` })),
            ]}
          />

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input
              label="الميزانية"
              type="number"
              step="0.01"
              value={form.budget}
              onChange={onChange('budget')}
            />
            <Input
              label="تاريخ البداية *"
              type="date"
              value={form.startDate}
              onChange={onChange('startDate')}
              required
            />
            <Input
              label="تاريخ النهاية"
              type="date"
              value={form.endDate}
              onChange={onChange('endDate')}
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ المشروع
            </Button>
            <Link href="/projects">
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
