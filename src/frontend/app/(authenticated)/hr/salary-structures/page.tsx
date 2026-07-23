'use client';

// صفحة قائمة هياكل الرواتب (Salary Structures)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, Pencil, Power, Banknote, RefreshCw, Hash } from 'lucide-react';
import { Button, Card, PageHeader, Table, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { salaryStructuresApi, SalaryStructure, getErrorMessage } from '@/lib/api';

export default function SalaryStructuresPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<SalaryStructure[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [deactivating, setDeactivating] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await salaryStructuresApi.list(includeInactive);
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل هياكل الرواتب.'));
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
    if (!window.confirm(`هل تريد إيقاف هيكل الراتب "${name}"؟`)) return;
    setDeactivating(id);
    try {
      await salaryStructuresApi.deactivate(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف هيكل الراتب.'));
    } finally {
      setDeactivating(null);
    }
  };

  const active = items.filter((s) => s.isActive).length;

  return (
    <div>
      <PageHeader
        title="💰 هياكل الرواتب"
        description="قوالب مكونات الراتب (الأساسي + البدلات + الخصومات) المستخدمة في دورات Payroll"
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
            <Link href="/hr/salary-structures/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
                هيكل جديد
              </Button>
            </Link>
          </div>
        }
      />

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
            <Banknote className="h-4 w-4" />
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
            render: (s) => (
              <span className="inline-flex items-center gap-1 font-mono text-sm text-gray-700">
                <Hash className="h-3 w-3 text-gray-400" />
                {s.code}
              </span>
            ),
          },
          {
            key: 'name',
            header: 'اسم الهيكل',
            render: (s) => (
              <Link
                href={`/hr/salary-structures/${s.id}`}
                className="font-semibold text-gray-800 hover:text-blue-600"
              >
                {s.name}
              </Link>
            ),
          },
          {
            key: 'currency',
            header: 'العملة',
            align: 'center',
            render: (s) => <Badge variant="info">{s.currency}</Badge>,
          },
          {
            key: 'lines',
            header: 'المكوّنات',
            align: 'center',
            render: (s) => (
              <span className="text-xs text-gray-700">
                {s.lines.length} سطر
              </span>
            ),
          },
          {
            key: 'totals',
            header: 'الإجمالي',
            align: 'end',
            render: (s) => (
              <div className="text-xs space-y-0.5">
                <div className="text-green-700">+{s.totalEarnings.toLocaleString()}</div>
                <div className="text-red-600">−{s.totalDeductions.toLocaleString()}</div>
              </div>
            ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (s) =>
              s.isActive ? (
                <Badge variant="success">نشط</Badge>
              ) : (
                <Badge variant="neutral">معطّل</Badge>
              ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (s) => (
              <div className="flex items-center gap-1 justify-center">
                <Link href={`/hr/salary-structures/${s.id}`}>
                  <Button
                    variant="ghost"
                    size="sm"
                    iconLeft={<Eye className="h-3.5 w-3.5" />}
                  >
                    عرض
                  </Button>
                </Link>
                <Link href={`/hr/salary-structures/${s.id}/edit`}>
                  <Button
                    variant="ghost"
                    size="sm"
                    iconLeft={<Pencil className="h-3.5 w-3.5" />}
                  >
                    تعديل
                  </Button>
                </Link>
                {s.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onDeactivate(s.id, s.name)}
                    loading={deactivating === s.id}
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
        rowKey={(s) => s.id}
        emptyMessage="لا توجد هياكل رواتب. أنشئ أول هيكل."
      />
    </div>
  );
}
