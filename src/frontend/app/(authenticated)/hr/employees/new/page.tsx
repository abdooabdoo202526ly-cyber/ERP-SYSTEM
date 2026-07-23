'use client';

// صفحة إنشاء موظف جديد (Employee) — form

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { hrApi, Department, getErrorMessage } from '@/lib/api';

interface FormState {
  fullName: string;
  email: string;
  phone: string;
  nationalId: string;
  departmentId: string;
  jobTitle: string;
  hireDate: string;
  baseSalary: number;
}

export default function NewEmployeePage() {
  const router = useRouter();
  useAuth();
  const [departments, setDepartments] = useState<Department[]>([]);
  const [form, setForm] = useState<FormState>({
    fullName: '',
    email: '',
    phone: '',
    nationalId: '',
    departmentId: '',
    jobTitle: '',
    hireDate: new Date().toISOString().slice(0, 10),
    baseSalary: 0,
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
      const depts = await hrApi.listDepartments();
      setDepartments(depts);
    } catch {
      // ignore — قد لا توجد أقسام بعد
    } finally {
      setLoading(false);
    }
  };

  const onChange = (k: keyof FormState) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    const v = e.target.value;
    setForm((f) => ({ ...f, [k]: k === 'baseSalary' ? Number(v) : v }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!form.fullName || !form.email) {
      setError('الاسم الكامل والبريد الإلكتروني مطلوبان.');
      return;
    }
    setSubmitting(true);
    try {
      await hrApi.createEmployee({
        ...form,
        isActive: true,
      });
      router.push('/hr/employees');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الموظف.'));
      setSubmitting(false);
    }
  };

  const deptOptions = [
    { label: '— بدون قسم —', value: '' },
    ...departments.map((d) => ({ label: d.name, value: d.id })),
  ];

  return (
    <div>
      <PageHeader
        title="➕ موظف جديد"
        description="أضف موظفاً جديداً إلى الشركة"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الموظفين', href: '/hr/employees' },
          { label: 'جديد' },
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
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
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
              disabled={loading}
            />
            <Input
              type="date"
              label="تاريخ التعيين"
              value={form.hireDate}
              onChange={onChange('hireDate')}
              required
            />
          </div>

          <Input
            label="الراتب الأساسي"
            type="number"
            min={0}
            step={0.01}
            value={form.baseSalary}
            onChange={onChange('baseSalary')}
            hint="للعرض فقط في هذه المرحلة (لا payroll)"
          />

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ الموظف
            </Button>
            <Link href="/hr/employees">
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
