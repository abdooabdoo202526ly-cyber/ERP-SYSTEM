'use client';

// تعديل قسم (Department Edit)

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { departmentsApi, Department, getErrorMessage } from '@/lib/api';

export default function EditDepartmentPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const [department, setDepartment] = useState<Department | null>(null);
  const [name, setName] = useState('');
  const [parentId, setParentId] = useState('');
  const [managerId, setManagerId] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const d = await departmentsApi.get(params.id);
      setDepartment(d);
      setName(d.name);
      setParentId(d.parentId || '');
      setManagerId(d.managerId || '');
      setIsActive(d.isActive);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل القسم.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!department) return;
    setError(null);
    setSubmitting(true);
    try {
      const updated = await departmentsApi.update(department.id, {
        name: name.trim(),
        parentId: parentId || undefined,
        managerId: managerId || undefined,
        isActive,
      });
      router.push(`/hr/departments/${updated.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث القسم.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="تعديل قسم" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!department) {
    return (
      <div>
        <PageHeader
          title="تعديل قسم"
          actions={
            <Link href="/hr/departments">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'القسم غير موجود.'}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل قسم"
        description={`تعديل بيانات: ${department.name}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الأقسام', href: '/hr/departments' },
          { label: department.name, href: `/hr/departments/${department.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/hr/departments/${department.id}`}>
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
          <div>
            <p className="text-xs text-gray-500 mb-1">الكود (غير قابل للتعديل)</p>
            <p className="font-mono text-sm text-gray-700 bg-gray-50 px-3 py-2 rounded border border-gray-200">
              {department.code}
            </p>
          </div>

          <Input
            label="اسم القسم *"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />

          <Input
            label="القسم الأب (Parent ID)"
            value={parentId}
            onChange={(e) => setParentId(e.target.value)}
            placeholder="اتركه فارغاً لقسم جذر"
          />

          <Input
            label="مدير القسم (Manager User ID)"
            value={managerId}
            onChange={(e) => setManagerId(e.target.value)}
          />

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span>نشط</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ
            </Button>
            <Link href={`/hr/departments/${department.id}`}>
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
