'use client';

// صفحة تعديل موظف (Employee Edit) — form مُعبَّأ من الـ API، يحفظ عبر PUT.

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  Department,
  UpdateEmployeePayload,
  getErrorMessage,
} from '@/lib/api';

export default function EditEmployeePage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();

  const [departments, setDepartments] = useState<Department[]>([]);
  const [form, setForm] = useState<UpdateEmployeePayload | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const [emp, depts] = await Promise.all([
        hrApi.getEmployee(params.id),
        hrApi.listDepartments().catch(() => [] as Department[]),
      ]);
      setDepartments(depts);
      setForm({
        fullName: emp.fullName,
        email: emp.email || '',
        phone: emp.phone || '',
        nationalId: emp.nationalId || '',
        departmentId: emp.departmentId || '',
        jobTitle: emp.jobTitle || '',
        hireDate: emp.hireDate ? emp.hireDate.slice(0, 10) : new Date().toISOString().slice(0, 10),
        terminationDate: emp.terminationDate ? emp.terminationDate.slice(0, 10) : undefined,
        baseSalary: emp.baseSalary,
        isActive: emp.isActive,
      });
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل الموظف.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onChange = (k: keyof UpdateEmployeePayload) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) =>
      f
        ? {
            ...f,
            [k]: k === 'baseSalary' ? Number(v) : v,
          }
        : f
    );
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setError(null);
    if (!form.fullName || !form.email) {
      setError('الاسم الكامل والبريد الإلكتروني مطلوبان.');
      return;
    }
    setSubmitting(true);
    try {
      await hrApi.updateEmployee(params.id, {
        ...form,
        // ensure terminationDate is null when empty string
        terminationDate: form.terminationDate || undefined,
        // departmentId: '' → undefined (backend expects Guid?; empty string غير مدعوم)
        departmentId: form.departmentId || undefined,
      });
      router.push(`/hr/employees/${params.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الموظف.'));
      setSubmitting(false);
    }
  };

  const deptOptions = [
    { label: '— بدون قسم —', value: '' },
    ...departments.map((d) => ({ label: d.name, value: d.id })),
  ];

  if (loading) {
    return (
      <div>
        <PageHeader title="✏️ تعديل موظف" />
        <Card className="max-w-2xl">
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!form) {
    return (
      <div>
        <PageHeader
          title="✏️ تعديل موظف"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الموظفين', href: '/hr/employees' },
            { label: 'تعديل' },
          ]}
          actions={
            <Link href="/hr/employees">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card className="max-w-2xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'الموظف غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/hr/employees">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل موظف"
        description={form.fullName}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الموظفين', href: '/hr/employees' },
          { label: form.fullName, href: `/hr/employees/${params.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/hr/employees/${params.id}`}>
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
          <Input
            label="الاسم الكامل *"
            value={form.fullName}
            onChange={onChange('fullName')}
            required
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="البريد الإلكتروني *"
              type="email"
              value={form.email}
              onChange={onChange('email')}
              required
            />
            <Input
              label="الهاتف"
              type="tel"
              value={form.phone}
              onChange={onChange('phone')}
              placeholder="+218 ..."
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="الرقم الوطني"
              value={form.nationalId}
              onChange={onChange('nationalId')}
              placeholder="اختياري"
            />
            <Input
              label="المسمى الوظيفي"
              value={form.jobTitle}
              onChange={onChange('jobTitle')}
              placeholder="مثال: محاسب"
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="القسم"
              value={form.departmentId}
              onChange={onChange('departmentId')}
              options={deptOptions}
            />
            <Input
              type="date"
              label="تاريخ التعيين"
              value={form.hireDate}
              onChange={onChange('hireDate')}
              required
            />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              type="date"
              label="تاريخ نهاية الخدمة"
              value={form.terminationDate || ''}
              onChange={onChange('terminationDate')}
              hint="اتركه فارغاً إذا كان الموظف لا يزال نشطاً"
            />
            <Input
              label="الراتب الأساسي"
              type="number"
              min={0}
              step={0.01}
              value={form.baseSalary}
              onChange={onChange('baseSalary')}
            />
          </div>

          <label className="flex items-center gap-2 text-sm pt-3 border-t">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              className="rounded border-gray-300"
            />
            <span>فعّال (نشط في الشركة)</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ التعديلات
            </Button>
            <Link href={`/hr/employees/${params.id}`}>
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
