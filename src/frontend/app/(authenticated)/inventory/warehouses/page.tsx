'use client';

// صفحة قائمة المخازن (Warehouses)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, Pencil, Power, Warehouse as WarehouseIcon, RefreshCw, MapPin } from 'lucide-react';
import { Button, Card, PageHeader, Table, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { warehousesApi, Warehouse, getErrorMessage } from '@/lib/api';

export default function WarehousesPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<Warehouse[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);
  const [deactivating, setDeactivating] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await warehousesApi.list(includeInactive);
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل المخازن.'));
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
    if (!window.confirm(`هل تريد إيقاف المخزن "${name}"؟`)) return;
    setDeactivating(id);
    try {
      await warehousesApi.deactivate(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف المخزن.'));
    } finally {
      setDeactivating(null);
    }
  };

  const active = items.filter((w) => w.isActive).length;

  return (
    <div>
      <PageHeader
        title="🏬 المخازن"
        description="إدارة المخازن (Warehouses) داخل الـ tenant"
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
            <Link href="/inventory/warehouses/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
                مخزن جديد
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
            <WarehouseIcon className="h-4 w-4" />
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
            render: (w) => <span className="font-mono text-sm text-gray-700">{w.code}</span>,
          },
          {
            key: 'name',
            header: 'اسم المخزن',
            render: (w) => (
              <Link
                href={`/inventory/warehouses/${w.id}`}
                className="font-semibold text-gray-800 hover:text-blue-600"
              >
                {w.name}
              </Link>
            ),
          },
          {
            key: 'location',
            header: 'الموقع',
            render: (w) =>
              w.location ? (
                <span className="inline-flex items-center gap-1 text-xs text-gray-600">
                  <MapPin className="h-3 w-3" />
                  {w.location}
                </span>
              ) : (
                <span className="text-gray-400 text-xs">—</span>
              ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (w) =>
              w.isActive ? (
                <Badge variant="success">نشط</Badge>
              ) : (
                <Badge variant="neutral">معطّل</Badge>
              ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (w) => (
              <div className="flex items-center gap-1 justify-center">
                <Link href={`/inventory/warehouses/${w.id}`}>
                  <Button
                    variant="ghost"
                    size="sm"
                    iconLeft={<Eye className="h-3.5 w-3.5" />}
                  >
                    عرض
                  </Button>
                </Link>
                {w.isActive && (
                  <Link href={`/inventory/warehouses/${w.id}/edit`}>
                    <Button
                      variant="ghost"
                      size="sm"
                      iconLeft={<Pencil className="h-3.5 w-3.5" />}
                    >
                      تعديل
                    </Button>
                  </Link>
                )}
                {w.isActive && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onDeactivate(w.id, w.name)}
                    loading={deactivating === w.id}
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
        rowKey={(w) => w.id}
        emptyMessage="لا توجد مخازن. أضف أول مخزن."
      />
    </div>
  );
}
