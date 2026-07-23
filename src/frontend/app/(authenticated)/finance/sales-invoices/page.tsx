'use client';

// صفحة قائمة فواتير المبيعات (Sales Invoices) — v1.0.32: pagination + search + status filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { Button, Table, Badge, PageHeader, SearchBar, Pagination, Select } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { arApi, SalesInvoice, SALES_INVOICE_STATUSES, SALES_INVOICE_STATUS_VARIANTS, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function SalesInvoicesPage() {
  const { loading: authLoading } = useAuth();
  const [invoices, setInvoices] = useState<SalesInvoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // v1.0.32: filters + pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('');
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
      const data = await arApi.listInvoices();
      setInvoices(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الفواتير.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return invoices.filter((i) => {
      if (statusFilter && String(i.status) !== statusFilter) return false;
      if (!q) return true;
      return (
        (i.invoiceNumber || '').toLowerCase().includes(q) ||
        (i.customerName || '').toLowerCase().includes(q) ||
        (i.notes || '').toLowerCase().includes(q)
      );
    });
  }, [invoices, search, statusFilter]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="🧾 فواتير المبيعات"
        description="قائمة فواتير المبيعات (AR Invoices)"
        actions={
          <Link href="/finance/sales-invoices/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              فاتورة جديدة
            </Button>
          </Link>
        }
      />

      {/* v1.0.32: Search + Status filter */}
      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (رقم الفاتورة / العميل / ملاحظات)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <Select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value)}
          options={[
            { value: '', label: 'كل الحالات' },
            ...Object.entries(SALES_INVOICE_STATUSES).map(([k, v]) => ({ value: k, label: v })),
          ]}
          className="w-48"
        />
        <span className="text-sm text-gray-500">{total} نتيجة</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'invoiceNumber',
            header: 'رقم الفاتورة',
            render: (i) => <span className="font-mono text-blue-600 font-semibold">{i.invoiceNumber || i.id?.substring(0, 8)}</span>,
          },
          {
            key: 'customer',
            header: 'العميل',
            render: (i) => i.customerName || <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'invoiceDate',
            header: 'التاريخ',
            render: (i) => <span className="text-sm">{formatDate(i.invoiceDate)}</span>,
          },
          {
            key: 'dueDate',
            header: 'الاستحقاق',
            render: (i) => i.dueDate ? <span className="text-sm">{formatDate(i.dueDate)}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'total',
            header: 'الإجمالي',
            align: 'end',
            render: (i) => <span className="font-mono font-bold">{formatNumber(i.totalAmount || 0)} {i.currencyCode || 'LYD'}</span>,
          },
          {
            key: 'outstanding',
            header: 'المتبقي',
            align: 'end',
            render: (i) => {
              const out = i.outstanding || 0;
              return (
                <span className={`font-mono text-sm font-semibold ${out > 0 ? 'text-red-600' : 'text-green-600'}`}>
                  {formatNumber(out)} {i.currencyCode || 'LYD'}
                </span>
              );
            },
          },
          {
            key: 'status',
            header: 'الحالة',
            align: 'center',
            render: (i) => <Badge variant={SALES_INVOICE_STATUS_VARIANTS[i.status] || 'neutral'}>{SALES_INVOICE_STATUSES[i.status] || i.status}</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (i) => (
              <div className="flex items-center gap-1 justify-center">
                <Link href={`/finance/sales-invoices/${i.id}`}>
                  <Button variant="ghost" size="sm">عرض</Button>
                </Link>
                <Link href={`/finance/sales-invoices/${i.id}/print`} target="_blank">
                  <Button variant="ghost" size="sm">🖨️</Button>
                </Link>
              </div>
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(i) => i.id}
        emptyMessage={search ? 'لا توجد فواتير تطابق البحث.' : 'لا توجد فواتير. أنشئ أول فاتورة.'}
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
