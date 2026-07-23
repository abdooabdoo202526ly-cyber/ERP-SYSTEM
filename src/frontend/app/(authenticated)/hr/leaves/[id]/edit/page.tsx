'use client';

// صفحة تعديل طلب إجازة (Leave Edit) — متاح فقط إذا الحالة Pending.
//   إذا كانت الحالة غير Pending، نُحوّل المستخدم إلى التفاصيل مع رسالة توضيح.

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Select, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  Employee,
  LeaveRequest,
  LEAVE_TYPES,
  UpdateLeaveRequestPayload,
  getErrorMessage,
} from '@/lib/api';

const LEAVE_TYPE_OPTIONS = Object.entries(LEAVE_TYPES).map(([k, v]) => ({
  label: v,
  value: Number(k),
}));

export default function EditLeavePage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();

  const [leave, setLeave] = useState<LeaveRequest | null>(null);
  const [employee, setEmployee] = useState<Employee | null>(null);
  const [form, setForm] = useState<UpdateLeaveRequestPayload | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [blockedReason, setBlockedReason] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    setBlockedReason(null);
    try {
      const l = await hrApi.getLeave(params.id);
      setLeave(l);

      // Guard: التعديل متاح فقط إذا Pending (status === 1).
      if (l.status !== 1) {
        setBlockedReason(
          l.status === 2
            ? 'لا يمكن تعديل طلب تمت الموافقة عليه.'
            : l.status === 3
              ? 'لا يمكن تعديل طلب مرفوض.'
              : 'لا يمكن تعديل طلب في حالته الحالية.'
        );
        setLoading(false);
        return;
      }

      // employee name lookup (best-effort)
      try {
        const emps = await hrApi.listEmployees();
        const e = emps.find((x) => x.id === l.employeeId) || null;
        setEmployee(e);
      } catch {
        setEmployee(null);
      }

      setForm({
        leaveType: l.leaveType,
        startDate: l.startDate ? l.startDate.slice(0, 10) : new Date().toISOString().slice(0, 10),
        endDate: l.endDate ? l.endDate.slice(0, 10) : new Date().toISOString().slice(0, 10),
        reason: l.reason || '',
        notes: l.notes || '',
      });
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل الطلب.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onChange = (k: keyof UpdateLeaveRequestPayload) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
    const v = e.target.value;
    setForm((f) => (f ? { ...f, [k]: k === 'leaveType' ? Number(v) : v } : f));
  };

  const totalDays = () => {
    if (!form?.startDate || !form?.endDate) return 0;
    const start = new Date(form.startDate);
    const end = new Date(form.endDate);
    const diff = Math.floor((end.getTime() - start.getTime()) / (1000 * 60 * 60 * 24)) + 1;
    return Math.max(0, diff);
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setError(null);
    if (new Date(form.endDate) < new Date(form.startDate)) {
      setError('تاريخ النهاية يجب أن يكون بعد أو يساوي تاريخ البداية.');
      return;
    }
    setSubmitting(true);
    try {
      await hrApi.updateLeave(params.id, form);
      router.push(`/hr/leaves/${params.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الطلب.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="✏️ تعديل طلب إجازة" />
        <Card className="max-w-2xl">
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (blockedReason) {
    return (
      <div>
        <PageHeader
          title="✏️ تعديل طلب إجازة"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الإجازات', href: '/hr/leaves' },
            { label: 'غير قابل للتعديل' },
          ]}
          actions={
            <Link href={`/hr/leaves/${params.id}`}>
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع للتفاصيل
              </Button>
            </Link>
          }
        />
        <Card className="max-w-2xl">
          <div className="bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-3 rounded-lg text-sm">
            {blockedReason}
          </div>
          <div className="mt-4">
            <Link href={`/hr/leaves/${params.id}`}>
              <Button variant="ghost">عرض تفاصيل الطلب</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  if (!form || !leave) {
    return (
      <div>
        <PageHeader
          title="✏️ تعديل طلب إجازة"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الإجازات', href: '/hr/leaves' },
            { label: 'تعديل' },
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
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'الطلب غير موجود.'}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل طلب إجازة"
        description={
          employee
            ? `${employee.fullName} • ${LEAVE_TYPES[form.leaveType] || form.leaveType}`
            : LEAVE_TYPES[form.leaveType] || form.leaveType
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الإجازات', href: '/hr/leaves' },
          { label: employee?.fullName || 'طلب', href: `/hr/leaves/${params.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/hr/leaves/${params.id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للتفاصيل
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
          {/* الموظف (read-only — لا يمكن نقل طلب إجازة لموظف آخر) */}
          <div>
            <p className="block text-sm font-medium text-gray-700 mb-1">الموظف</p>
            <div className="px-3 py-2 rounded-lg bg-gray-50 border border-gray-200 text-sm text-gray-700">
              {employee ? (
                <Link
                  href={`/hr/employees/${employee.id}`}
                  className="text-blue-600 hover:underline font-semibold"
                >
                  {employee.fullName}
                </Link>
              ) : (
                <span>{leave.employeeName || leave.employeeId}</span>
              )}
              <p className="text-[10px] text-gray-400 mt-1">
                لا يمكن نقل الطلب إلى موظف آخر — أنشئ طلباً جديداً إن لزم.
              </p>
            </div>
          </div>

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

          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظات</label>
            <textarea
              value={form.notes}
              onChange={onChange('notes')}
              rows={2}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
              placeholder="ملاحظات إضافية (اختياري)"
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ التعديلات
            </Button>
            <Link href={`/hr/leaves/${params.id}`}>
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
