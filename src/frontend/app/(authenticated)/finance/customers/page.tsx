'use client';

// صفحة قائمة العملاء (Customers) — v1.0.32: pagination + search + status filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus, Mail, Phone, MapPin, CreditCard } from 'lucide-react';
import { Button, Input, Table, Badge, PageHeader, EntityActions, SearchBar, Pagination, Select } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { arApi, Customer, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function CustomersPage() {
  const { loading: authLoading } = useAuth();
  const [customers, setCustomers] = useState<Customer[]>([]);
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

  // Reset page when search/filter changes
  useEffect(() => {
    setPage(1);
  }, [search, statusFilter]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await arApi.listCustomers();
      setCustomers(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل العملاء. تأكد أن الـ backend يعمل وأن endpoint /api/ar/customers جاهز.'));
    } finally {
      setLoading(false);
    }
  };

  // Client-side filtering
  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return customers.filter((c) => {
      if (statusFilter === 'active' && !c.isActive) return false;
      if (statusFilter === 'inactive' && c.isActive) return false;
      if (!q) return true;
      return (
        c.name.toLowerCase().includes(q) ||
        (c.code || '').toLowerCase().includes(q) ||
        (c.email || '').toLowerCase().includes(q) ||
        (c.phone || '').toLowerCase().includes(q)
      );
    });
  }, [customers, search, statusFilter]);

  // Pagination (client-side)
  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="👥 العملاء"
        description="قائمة العملاء المُسجَّلين في النظام"
        actions={
          <Link href="/finance/customers/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              عميل جديد
            </Button>
          </Link>
        }
      />

      {/* v1.0.32: Search + Status filter */}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (اسم / كود / بريد / هاتف)..."
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
        <span className="text-sm text-gray-500">
          {total} نتيجة
        </span>
      </div>

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
            render: (c) => <span className="font-mono text-sm">{c.code}</span>,
          },
          {
            key: 'name',
            header: 'اسم العميل',
            render: (c) => (
              <div>
                <Link
                  href={`/finance/customers/${c.id}`}
                  className="font-semibold text-gray-800 hover:text-blue-600"
                >
                  {c.name}
                </Link>
                {c.nameEn && <p className="text-xs text-gray-500">{c.nameEn}</p>}
              </div>
            ),
          },
          {
            key: 'contact',
            header: 'الاتصال',
            render: (c) => (
              <div className="text-xs space-y-0.5">
                {c.email && <p className="text-gray-600 flex items-center gap-1"><Mail className="h-3 w-3" />{c.email}</p>}
                {c.phone && <p className="text-gray-600 flex items-center gap-1"><Phone className="h-3 w-3" />{c.phone}</p>}
              </div>
            ),
          },
          {
            key: 'taxId',
            header: 'الرقم الضريبي',
            render: (c) => c.taxId ? <span className="font-mono text-xs">{c.taxId}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'creditLimit',
            header: 'حد الائتمان',
            align: 'end',
            render: (c) => c.creditLimit ? <span className="font-mono text-sm">{formatNumber(c.creditLimit)}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'balance',
            header: 'الرصيد',
            align: 'end',
            render: (c: any) => (
              <span className={`font-mono text-sm font-semibold ${(c.balance || 0) > 0 ? 'text-red-600' : 'text-gray-700'}`}>
                {formatNumber(c.balance || 0)}
              </span>
            ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (c) => c.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">معطّل</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (c) => (
              <div className="flex items-center gap-1 justify-center">
                <EntityActions
                  itemLabel={c.name}
                  editHref={`/finance/customers/${c.id}/edit`}
                  onDelete={async () => { await arApi.deactivateCustomer(c.id); await load(); }}
                />
              </div>
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(c) => c.id}
        emptyMessage={search ? 'لا توجد نتائج تطابق البحث.' : 'لا توجد عملاء. أضف أول عميل.'}
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
