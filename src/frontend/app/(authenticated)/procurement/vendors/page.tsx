'use client';

// صفحة قائمة الموردين (Vendors) — v1.0.32: pagination + search + status filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus, Mail, Phone } from 'lucide-react';
import { Button, Table, Badge, PageHeader, EntityActions, SearchBar, Pagination, Select } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { procurementApi, Vendor, getErrorMessage } from '@/lib/api';

export default function VendorsPage() {
  const { loading: authLoading } = useAuth();
  const [vendors, setVendors] = useState<Vendor[]>([]);
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
      const data = await procurementApi.listVendors();
      setVendors(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الموردين.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return vendors.filter((v) => {
      if (statusFilter === 'active' && !v.isActive) return false;
      if (statusFilter === 'inactive' && v.isActive) return false;
      if (!q) return true;
      return (
        (v.name || '').toLowerCase().includes(q) ||
        (v.code || '').toLowerCase().includes(q) ||
        (v.email || '').toLowerCase().includes(q) ||
        (v.phone || '').toLowerCase().includes(q) ||
        (v.taxNumber || '').toLowerCase().includes(q)
      );
    });
  }, [vendors, search, statusFilter]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="🚚 الموردون"
        description="قائمة الموردين (AP Vendors)"
        actions={
          <Link href="/procurement/vendors/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>مورد جديد</Button>
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (اسم / كود / بريد / هاتف / رقم ضريبي)..."
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
            key: 'code',
            header: 'الكود',
            render: (v) => v.code ? <span className="font-mono text-sm">{v.code}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'name',
            header: 'اسم المورد',
            render: (v) => (
              <div>
                <Link href={`/procurement/vendors/${v.id}`} className="font-semibold text-gray-800 hover:text-blue-600">
                  {v.name}
                </Link>
              </div>
            ),
          },
          {
            key: 'contact',
            header: 'الاتصال',
            render: (v) => (
              <div className="text-xs space-y-0.5">
                {v.email && <p className="text-gray-600 flex items-center gap-1"><Mail className="h-3 w-3" />{v.email}</p>}
                {v.phone && <p className="text-gray-600 flex items-center gap-1"><Phone className="h-3 w-3" />{v.phone}</p>}
              </div>
            ),
          },
          {
            key: 'taxNumber',
            header: 'الرقم الضريبي',
            render: (v) => v.taxNumber ? <span className="font-mono text-xs">{v.taxNumber}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'paymentTerms',
            header: 'شروط الدفع',
            render: (v) => <span className="text-xs">{v.paymentTerms || '—'}</span>,
          },
          {
            key: 'currency',
            header: 'العملة',
            align: 'center',
            render: (v) => <Badge variant="info">{v.currency || 'LYD'}</Badge>,
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (v) => v.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">معطّل</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (v) => (
              <EntityActions
                itemLabel={v.name}
                editHref={`/procurement/vendors/${v.id}/edit`}
                onDelete={async () => { await procurementApi.deactivateVendor(v.id); await load(); }}
              />
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(v) => v.id}
        emptyMessage={search ? 'لا توجد نتائج تطابق البحث.' : 'لا يوجد موردون.'}
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
