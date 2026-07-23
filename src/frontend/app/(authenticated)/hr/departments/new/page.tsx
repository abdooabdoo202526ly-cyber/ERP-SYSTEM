'use client';

// إنشاء قسم جديد (Department)

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { departmentsApi, hrApi, getErrorMessage, Department } from '@/lib/api';

export default function NewDepartmentPage() {
  const router = useRouter();
  useAuth();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [parentId, setParentId] = useState('');
  const [managerId, setManagerId] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // v1.0.24: dropdowns for Parent + Manager
  const [departments, setDepartments] = useState<Department[]>([]);
  const [employees, setEmployees] = useState<{ id: string; employeeNumber: string; fullName: string }[]>([]);
  useEffect(() => {
    departmentsApi.list(true).then(setDepartments).catch(() => setDepartments([]));
    hrApi.listEmployees().then((list) => setEmployees(list.map((e) => ({ id: e.id, employeeNumber: e.employeeNumber, fullName: e.fullName })))).catch(() => setEmployees([]));
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const created = await departmentsApi.create({
        code: code.trim(),
        name: name.trim(),
        parentId: parentId || undefined,
        managerId: managerId || undefined,
      });
      router.push(`/hr/departments/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء القسم.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ قسم جديد"
        description="إنشاء قسم تنظيمي جديد (Department)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الأقسام', href: '/hr/departments' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/hr/departments">
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
              label="الكود *"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              placeholder="مثال: HR"
            />
            <Input
              label="اسم القسم *"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              placeholder="مثال: الموارد البشرية"
            />
          </div>
          <Select
            label="القسم الأب"
            value={parentId}
            onChange={(e) => setParentId(e.target.value)}
            options={[
              { value: '', label: departments.length === 0 ? 'لا توجد أقسام' : '— بدون قسم أب (مستوى أعلى) —' },
              ...departments.map((d) => ({ value: d.id, label: `${d.code} — ${d.name}` })),
            ]}
          />
          <Select
            label="مدير القسم"
            value={managerId}
            onChange={(e) => setManagerId(e.target.value)}
            options={[
              { value: '', label: employees.length === 0 ? 'لا يوجد موظفين — أضف موظف أولاً' : '— بدون مدير —' },
              ...employees.map((e) => ({ value: e.id, label: `${e.employeeNumber} — ${e.fullName}` })),
            ]}
          />

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ
            </Button>
            <Link href="/hr/departments">
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
