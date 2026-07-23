'use client';

// صفحة المنتجات (Items) — v1.0.32: pagination + search + status filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { Input, Card, PageHeader, Button, EntityActions, SearchBar, Pagination, Select, Table, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { inventoryApi, Item, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function ItemsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<Item[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // v1.0.32: filters + pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  useEffect(() => { setPage(1); }, [search, statusFilter]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await inventoryApi.listItems();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل المنتجات.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return items.filter((i: any) => {
      if (statusFilter === 'active' && !i.isActive) return false;
      if (statusFilter === 'inactive' && i.isActive) return false;
      if (!q) return true;
      return (
        (i.sku || '').toLowerCase().includes(q) ||
        (i.name || '').toLowerCase().includes(q) ||
        (i.nameEn || '').toLowerCase().includes(q) ||
        (i.categoryName || '').toLowerCase().includes(q) ||
        (i.barcode || '').toLowerCase().includes(q)
      );
    });
  }, [items, search, statusFilter]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="📦 المنتجات (الأصناف)"
        description="قائمة المنتجات المسجلة"
        actions={
          <Link href="/inventory/items/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>منتج جديد</Button>
          </Link>
        }
      />

      {/* v1.0.32: Search + Status filter */}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (SKU / اسم / كود / باركود)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <Select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as 'all' | 'active' | 'inactive')}
          options={[
            { value: 'all', label: 'الكل' },
            { value: 'active', label: 'نشط فقط' },
            { value: 'inactive', label: 'معطّل فقط' },
          ]}
          className="w-40"
        />
        <span className="text-sm text-gray-500">{total} نتيجة</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'sku',
            header: 'SKU',
            render: (i: any) => <span className="font-mono text-sm font-semibold">{i.sku}</span>,
          },
          {
            key: 'name',
            header: 'اسم المنتج',
            render: (i: any) => (
              <div>
                <Link href={`/inventory/items/${i.id}`} className="font-semibold text-gray-800 hover:text-blue-600">
                  {i.name}
                </Link>
                {i.nameEn && <p className="text-xs text-gray-500">{i.nameEn}</p>}
              </div>
            ),
          },
          {
            key: 'category',
            header: 'الفئة',
            render: (i: any) => i.categoryName || <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'unit',
            header: 'الوحدة',
            render: (i: any) => <span className="text-xs">{i.unitOfMeasureName || i.unitOfMeasure || '—'}</span>,
          },
          {
            key: 'cost',
            header: 'التكلفة',
            align: 'end',
            render: (i: any) => <span className="font-mono text-sm">{formatNumber(i.averageCost || i.standardCost || 0)}</span>,
          },
          {
            key: 'price',
            header: 'سعر البيع',
            align: 'end',
            render: (i: any) => <span className="font-mono text-sm font-bold text-green-700">{formatNumber(i.sellPrice || i.price || 0)}</span>,
          },
          {
            key: 'stock',
            header: 'المخزون',
            align: 'end',
            render: (i: any) => {
              const qty = i.quantityOnHand || 0;
              const min = i.reorderLevel || i.reorderPoint || 0;
              return (
                <span className={`font-mono text-sm font-semibold ${qty <= min ? 'text-red-600' : 'text-gray-700'}`}>
                  {formatNumber(qty)}
                  {min > 0 && <span className="text-xs text-gray-400"> / {formatNumber(min)}</span>}
                </span>
              );
            },
          },
          {
            key: 'status',
            header: 'الحالة',
            align: 'center',
            render: (i: any) => i.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">معطّل</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (i: any) => (
              <EntityActions
                itemLabel={i.name}
                editHref={`/inventory/items/${i.id}/edit`}
                onDelete={async () => { await inventoryApi.deleteItem(i.id); await load(); }}
              />
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(i) => i.id}
        emptyMessage={search ? 'لا توجد منتجات تطابق البحث.' : 'لا توجد منتجات.'}
      />

      <Pagination
        total={total}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  );
}
