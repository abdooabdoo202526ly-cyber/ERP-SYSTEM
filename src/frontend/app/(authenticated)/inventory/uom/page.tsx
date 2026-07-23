'use client';

// صفحة قائمة وحدات القياس (Units of Measure)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, Ruler, RefreshCw, Hash } from 'lucide-react';
import { Button, Card, PageHeader, Table, Badge, EntityActions } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { uomApi, UnitOfMeasure, getErrorMessage } from '@/lib/api';

export default function UomPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<UnitOfMeasure[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [includeInactive, setIncludeInactive] = useState(false);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await uomApi.list(includeInactive);
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل وحدات القياس.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (authLoading) return;
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authLoading, includeInactive]);

  const active = items.filter((u) => u.isActive).length;

  return (
    <div>
      <PageHeader
        title="📏 وحدات القياس (UoM)"
        description="إدارة وحدات القياس (قطعة، كجم، متر، لتر، ...)"
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
            <Link href="/inventory/uom/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
                وحدة جديدة
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
            <Ruler className="h-4 w-4" />
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
            render: (u) => (
              <span className="inline-flex items-center gap-1 font-mono text-sm text-gray-700">
                <Hash className="h-3 w-3 text-gray-400" />
                {u.code}
              </span>
            ),
          },
          {
            key: 'name',
            header: 'الاسم',
            render: (u) => (
              <Link
                href={`/inventory/uom/${u.id}`}
                className="font-semibold text-gray-800 hover:text-blue-600"
              >
                {u.name}
              </Link>
            ),
          },
          {
            key: 'symbol',
            header: 'الرمز',
            align: 'center',
            render: (u) =>
              u.symbol ? (
                <Badge variant="info">{u.symbol}</Badge>
              ) : (
                <span className="text-gray-400 text-xs">—</span>
              ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (u) =>
              u.isActive ? (
                <Badge variant="success">نشط</Badge>
              ) : (
                <Badge variant="neutral">معطّل</Badge>
              ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (u) => (
              <EntityActions
                editHref={`/inventory/uom/${u.id}/edit`}
                itemLabel={`${u.code} — ${u.name}`}
                onDelete={async () => {
                  try {
                    await uomApi.deactivate(u.id);
                    await load();
                  } catch (e) {
                    setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
                  }
                }}
              />
            ),
          },
        ]}
        data={items}
        loading={loading}
        rowKey={(u) => u.id}
        emptyMessage="لا توجد وحدات قياس. أضف أول وحدة."
      />
    </div>
  );
}
