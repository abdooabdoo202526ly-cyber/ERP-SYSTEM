'use client';

// صفحة قائمة الأقسام (Departments) — جدول بسيط + IncludeInactive filter

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, Pencil, Power, Users, RefreshCw } from 'lucide-react';
import { Button, Card, PageHeader, Table, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { departmentsApi, Department, getErrorMessage } from '@/lib/api';

export default function DepartmentsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<Department[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [deactivating, setDeactivating] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await departmentsApi.list(includeInactive);
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل الأقسام.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (authLoading) return;
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authLoading, includeInactive]);

  const onDeactivate = async (id: string, name: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف القسم "${name}"؟`)) return;
    setDeactivating(id);
    try {
      await departmentsApi.deactivate(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف القسم.'));
    } finally {
      setDeactivating(null);
    }
  };

  const active = items.filter((d) => d.isActive).length;

  return (
    <div>
      <PageHeader
        title="🏢 الأقسام"
        description="إدارة الأقسام التنظيمية داخل الشركة"
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={load}
              iconLeft={<RefreshCw className="h-4 w-4" />}
            >
              تحديث
            </Button>
            <Link href="/hr/departments/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
                قسم جديد
              </Button>
            </Link>
          </div>
        }
      />

      {/* Filters + Stats */}
      <Card className="mb-4 !p-0">
        <div className="p-4 flex flex-wrap items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={includeInactive}
              onChange={(e) => setIncludeInactive(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span>عرض المعطّلة</span>
          </label>
          <div className="flex items-center gap-1 text-sm text-gray-500">
            <Users className="h-4 w-4" />
            <span>
              {active} نشط / {items.length} إجمالي
            </span>
          </div>
        </div>
      </Card>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      <Table
        columns={[
          {
            key: 'code',
            header: 'الكود',
            render: (d) => <span className="font-mono text-sm text-gray-700">{d.code}</span>,
          },
          {
            key: 'name',
            header: 'اسم القسم',
            render: (d) => (
              <Link
                href={`/hr/departments/${d.id}`}
                className="font-semibold text-gray-800 hover:text-blue-600"
              >
                {d.name}
              </Link>
            ),
          },
          {
            key: 'parentId',
            header: 'القسم الأب',
            render: (d) =>
              d.parentId ? (
                <span className="text-xs font-mono text-gray-500">
                  {d.parentId.substring(0, 8)}…
                </span>
              ) : (
                <span className="text-gray-400 text-xs">— جذر —</span>
              ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (d) =>
              d.isActive ? (
                <Badge variant="success">نشط</Badge>
              ) : (
                <Badge variant="neutral">معطّل</Badge>
              ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (d) => (
              <div className="flex items-center gap-1 justify-center">
                <Link href={`/hr/departments/${d.id}`}>
                  <Button
                    variant="ghost"
                    size="sm"
                    iconLeft={<Eye className="h-3.5 w-3.5" />}
                  >
                    عرض
                  </Button>
                </Link>
                {d.isActive && (
                  <Link href={`/hr/departments/${d.id}/edit`}>
                    <Button
                      variant="ghost"
                      size="sm"
                      iconLeft={<Pencil className="h-3.5 w-3.5" />}
                    >
                      تعديل
                    </Button>
                  </Link>
                )}
                {d.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onDeactivate(d.id, d.name)}
                    loading={deactivating === d.id}
                    iconLeft={<Power className="h-3.5 w-3.5 text-red-500" />}
                  >
                    <span className="text-red-600 text-xs">إيقاف</span>
                  </Button>
                )}
              </div>
            ),
          },
        ]}
        data={items}
        loading={loading}
        rowKey={(d) => d.id}
        emptyMessage="لا توجد أقسام. أضف أول قسم."
      />
    </div>
  );
}
