'use client';

// صفحة إنشاء طلب إجازة جديد (Leave Request) — form

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Select, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  Employee,
  LeaveRequest,
  LEAVE_TYPES,
  getErrorMessage,
} from '@/lib/api';

interface FormState {
  employeeId: string;
  leaveType: number;
  startDate: string;
  endDate: string;
  reason: string;
}

const LEAVE_TYPE_OPTIONS = Object.entries(LEAVE_TYPES).map(([k, v]) => ({
  label: v,
  value: Number(k),
}));

export default function NewLeavePage() {
  const router = useRouter();
  const { user } = useAuth();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [form, setForm] = useState<FormState>({
    employeeId: '',
    leaveType: 1,
    startDate: new Date().toISOString().slice(0, 10),
    endDate: new Date().toISOString().slice(0, 10),
    reason: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    load();
  }, []);

  const load = async () => {
    setLoading(true);
    try {
      const data = await hrApi.listEmployees();
      setEmployees(data);
      // حاول اختيار المستخدم الحالي
      if (user?.email) {
        const me = data.find((e) => e.email === user.email);
        if (me) setForm((f) => ({ ...f, employeeId: me.id }));
      }
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  };

  const onChange = (k: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'leaveType' ? Number(v) : v }));
  };

  const totalDays = () => {
    if (!form.startDate || !form.endDate) return 0;
    const start = new Date(form.startDate);
    const end = new Date(form.endDate);
    const diff = Math.floor((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1;
    return Math.max(0, diff);
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!form.employeeId) {
      setError('يجب اختيار الموظف.');
      return;
    }
    if (new Date(form.endDate) < new Date(form.startDate)) {
      setError('تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.');
      return;
    }
    setSubmitting(true);
    try {
      const days = totalDays();
      const payload: Partial<LeaveRequest> = {
        ...form,
        totalDays: days,
        status: 1, // Pending
      };
      await hrApi.createLeave(payload);
      router.push('/hr/leaves');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الطلب.'));
      setSubmitting(false);
    }
  };

  const employeeOptions = employees.map((e) => ({
    label: `${e.fullName} (${e.employeeNumber})`,
    value: e.id,
  }));

  return (
    <div>
      <PageHeader
        title="➕ طلب إجازة جديد"
        description="إنشاء Leave Request"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الإجازات', href: '/hr/leaves' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/hr/leaves">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <Select
            label="الموظف *"
            value={form.employeeId}
            onChange={onChange('employeeId')}
            options={employeeOptions}
            placeholder={loading ? 'جاري التحميل...' : 'اختر الموظف'}
            required
            disabled={loading}
          />

          <Select
            label="نوع الإجازة *"
            value={form.leaveType}
            onChange={onChange('leaveType')}
            options={LEAVE_TYPE_OPTIONS}
            required
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              type="date"
              label="تاريخ البداية *"
              value={form.startDate}
              onChange={onChange('startDate')}
              required
            />
            <Input
              type="date"
              label="تاريخ النهاية *"
              value={form.endDate}
              onChange={onChange('endDate')}
              required
            />
          </div>

          <div className="bg-blue-50 border border-blue-200 rounded-lg px-4 py-2 text-sm text-blue-800">
            المدة الإجمالية: <span className="font-bold">{totalDays()}</span> يوم
          </div>

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">السبب</label>
            <textarea
              value={form.reason}
              onChange={onChange('reason')}
              rows={3}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
              placeholder="سبب الإجازة (اختياري)"
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              تقديم الطلب
            </Button>
            <Link href="/hr/leaves">
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
