'use client';

// صفحة سندات القبض (Receipts) — جدول

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus, CheckCircle2, RotateCcw } from 'lucide-react';
import { Button, Input, Table, Badge, PageHeader, Card } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { arApi, Receipt, PAYMENT_METHODS, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function ReceiptsPage() {
  const { loading: authLoading } = useAuth();
  const [receipts, setReceipts] = useState<Receipt[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await arApi.listReceipts();
      setReceipts(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل سندات القبض.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    if (!search) return receipts;
    const q = search.toLowerCase();
    return receipts.filter(
      (r) => r.receiptNumber.toLowerCase().includes(q) || (r.customerName || '').toLowerCase().includes(q)
    );
  }, [receipts, search]);

  const postReceipt = async (id: string) => {
    if (!confirm('سيتم ترحيل سند القبض وإنشاء قيد محاسبي (Dr 1210 / Cr 1230). هل أنت متأكد؟')) return;
    try {
      await arApi.postReceipt(id);
      await load();
    } catch (e: unknown) {
      alert(getErrorMessage(e, 'فشل ترحيل السند.'));
    }
  };

  const reverseReceipt = async (id: string) => {
    if (!confirm('سيتم عكس السند وإنشاء قيد عكسي. هل أنت متأكد؟')) return;
    try {
      await arApi.reverseReceipt(id);
      await load();
    } catch (e: unknown) {
      alert(getErrorMessage(e, 'فشل عكس السند.'));
    }
  };

  return (
    <div>
      <PageHeader
        title="💰 سندات القبض"
        description="سندات القبض على العملاء"
        actions={
          <Link href="/finance/receipts/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>سند قبض جديد</Button>
          </Link>
        }
      />

      <Card className="mb-4">
        <Input
          placeholder="🔍 بحث (رقم/عميل)..."
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
      </Card>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'receiptNumber',
            header: 'رقم السند',
            render: (r) => <span className="font-mono font-semibold text-blue-600">{r.receiptNumber}</span>,
          },
          {
            key: 'customer',
            header: 'العميل',
            render: (r) => <span className="font-semibold text-gray-800">{r.customerName || '—'}</span>,
          },
          {
            key: 'receiptDate',
            header: 'التاريخ',
            render: (r) => <span className="text-sm text-gray-600">{formatDate(r.receiptDate)}</span>,
          },
          {
            key: 'amount',
            header: 'المبلغ',
            align: 'end',
            render: (r) => <span className="font-mono font-bold">{formatNumber(r.amount)} {r.currencyCode}</span>,
          },
          {
            key: 'paymentMethod',
            header: 'الطريقة',
            render: (r) => r.paymentMethod ? <Badge variant="info">{PAYMENT_METHODS[r.paymentMethod] || r.paymentMethod}</Badge> : <span className="text-xs text-gray-400">—</span>,
          },
          {
            key: 'allocations',
            header: 'التخصيصات',
            align: 'center',
            render: (r) => <span className="text-sm text-gray-600">{r.allocations.length} فاتورة</span>,
          },
          {
            key: 'status',
            header: 'الحالة',
            align: 'center',
            render: (r) => r.postedAt ? <Badge variant="success">مُرحّل</Badge> : <Badge variant="warning">مسودة</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (r) => (
              <div className="flex items-center gap-1 justify-center">
                {!r.postedAt && (
                  <button onClick={() => postReceipt(r.id)} className="text-green-600 hover:text-green-800 p-1" title="ترحيل">
                    <CheckCircle2 className="h-4 w-4" />
                  </button>
                )}
                {r.postedAt && (
                  <button onClick={() => reverseReceipt(r.id)} className="text-orange-600 hover:text-orange-800 p-1" title="عكس">
                    <RotateCcw className="h-4 w-4" />
                  </button>
                )}
              </div>
            ),
          },
        ]}
        data={filtered}
        loading={loading}
        rowKey={(r) => r.id}
        emptyMessage="لا توجد سندات قبض."
      />

      {!loading && filtered.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">
          {filtered.length} سند • إجمالي: <span className="font-mono font-semibold">{formatNumber(filtered.reduce((s, r) => s + r.amount, 0))}</span>
        </p>
      )}
    </div>
  );
}
