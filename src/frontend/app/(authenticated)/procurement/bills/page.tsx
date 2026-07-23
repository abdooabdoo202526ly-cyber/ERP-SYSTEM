'use client';

// صفحة قائمة فواتير الموردين (Vendor Bills) — جدول

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { Button, Table, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { formatDate } from '@/lib/utils';
import {
  procurementApi,
  VendorBill,
  BILL_STATUSES,
  BILL_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

export default function BillsPage() {
  const { loading: authLoading } = useAuth();
  const [bills, setBills] = useState<VendorBill[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await procurementApi.listBills();
      setBills(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل فواتير الموردين.'));
    } finally {
      setLoading(false);
    }
  };

  const totalPayable = bills
    .filter((b) => b.status !== 4) // exclude cancelled
    .reduce((sum, b) => sum + b.totalAmount, 0);

  return (
    <div>
      <PageHeader
        title="🧾 فواتير الموردين"
        description="Vendor Bills — فواتير مستحقة الدفع"
        actions={
          <Link href="/procurement/bills/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              فاتورة جديدة
            </Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {!loading && bills.length > 0 && (
        <div className="bg-blue-50 border border-blue-200 rounded-xl p-4 mb-4 flex items-center justify-between">
          <div>
            <p className="text-sm text-blue-700 font-semibold">إجمالي المستحق (غير ملغي):</p>
            <p className="text-2xl font-bold text-blue-900 mt-1">{totalPayable.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
          </div>
          <span className="text-xs text-blue-600">من {bills.filter((b) => b.status !== 4).length} فاتورة</span>
        </div>
      )}

      <Table
        columns={[
          {
            key: 'billNumber',
            header: 'رقم الفاتورة',
            render: (b) => <span className="font-mono text-blue-600 font-semibold">{b.billNumber}</span>,
          },
          {
            key: 'gr',
            header: 'استلام البضاعة',
            render: (b) => b.grNumber || <span className="text-gray-400 text-xs">{b.goodsReceiptId}</span>,
          },
          {
            key: 'vendor',
            header: 'المورّد',
            render: (b) => b.vendorName || <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'billDate',
            header: 'تاريخ الفاتورة',
            render: (b) => (
              <span className="text-sm text-gray-700">
                {formatDate(b.billDate)}
              </span>
            ),
          },
          {
            key: 'dueDate',
            header: 'تاريخ الاستحقاق',
            render: (b) =>
              b.dueDate ? (
                <span className="text-sm text-gray-700">{formatDate(b.dueDate)}</span>
              ) : (
                <span className="text-gray-400 text-xs">—</span>
              ),
          },
          {
            key: 'total',
            header: 'الإجمالي',
            align: 'end',
            render: (b) => (
              <div className="text-end">
                <p className="font-bold text-gray-800">
                  {b.totalAmount?.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
                <p className="text-[10px] text-gray-500 font-mono">
                  {b.currency} (ضريبة: {b.taxAmount?.toFixed(2) || '0.00'})
                </p>
              </div>
            ),
          },
          {
            key: 'status',
            header: 'الحالة',
            render: (b) => (
              <Badge variant={BILL_STATUS_VARIANTS[b.status] || 'neutral'}>
                {BILL_STATUSES[b.status] || b.status}
              </Badge>
            ),
          },
        ]}
        data={bills}
        loading={loading}
        rowKey={(b) => b.id}
        emptyMessage="لا توجد فواتير موردين بعد."
      />

      {!loading && bills.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">{bills.length} فاتورة</p>
      )}
    </div>
  );
}

