'use client';

// صفحة قائمة استلامات البضاعة (Goods Receipts) — جدول + actions (Receive / Cancel)

import { useEffect, useState } from 'react';
import { formatDate } from '@/lib/utils';
import Link from 'next/link';
import { Plus, Send, X, Eye } from 'lucide-react';
import { Button, Table, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  GoodsReceipt,
  GR_STATUSES,
  GR_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

export default function GoodsReceiptsPage() {
  const { loading: authLoading } = useAuth();
  const [grs, setGRs] = useState<GoodsReceipt[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await procurementApi.listGRs();
      setGRs(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل استلامات البضاعة.'));
    } finally {
      setLoading(false);
    }
  };

  const onReceive = async (id: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm('هل تريد تأكيد استلام هذه البضاعة؟\nسيتم إنشاء حركات مخزون وترحيلها تلقائياً.')) return;
    setBusy(id);
    setError(null);
    try {
      await procurementApi.receiveGR(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تأكيد الاستلام.'));
    } finally {
      setBusy(null);
    }
  };

  const onCancel = async (id: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm('هل تريد إلغاء هذا الـ GR؟ (متاح فقط للحالة Draft)')) return;
    setBusy(id);
    setError(null);
    try {
      await procurementApi.cancelGR(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الإلغاء.'));
    } finally {
      setBusy(null);
    }
  };

  return (
    <div>
      <PageHeader
        title="📦 استلامات البضاعة"
        description="قائمة Goods Receipts (GR) — استلامات من أوامر الشراء"
        actions={
          <Link href="/procurement/goods-receipts/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              استلام جديد
            </Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      <Table
        columns={[
          {
            key: 'grNumber',
            header: 'رقم الاستلام',
            render: (g) => <span className="font-mono text-blue-600 font-semibold">{g.grNumber}</span>,
          },
          {
            key: 'po',
            header: 'أمر الشراء',
            render: (g) => g.poNumber || <span className="text-gray-400 text-xs">{g.purchaseOrderId}</span>,
          },
          {
            key: 'vendor',
            header: 'المورّد',
            render: (g) => g.vendorName || <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'warehouse',
            header: 'المستودع',
            render: (g) => g.warehouseName || <span className="text-gray-400 text-xs">{g.warehouseId}</span>,
          },
          {
            key: 'receivedDate',
            header: 'تاريخ الاستلام',
            render: (g) => (
              <span className="text-sm text-gray-700">
                {formatDate(g.receivedDate)}
              </span>
            ),
          },
          {
            key: 'lines',
            header: 'عدد البنود',
            align: 'center',
            render: (g) => <Badge variant="neutral">{g.lines?.length || 0}</Badge>,
          },
          {
            key: 'status',
            header: 'الحالة',
            render: (g) => (
              <Badge variant={GR_STATUS_VARIANTS[g.status] || 'neutral'}>
                {GR_STATUSES[g.status] || g.status}
              </Badge>
            ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (g) => (
              <div className="flex items-center gap-1 justify-center">
                <Link href={`/procurement/goods-receipts/${g.id}`}>
                  <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3.5 w-3.5" />}>
                    عرض
                  </Button>
                </Link>
                {/* Receive: Draft (1) only */}
                {g.status === 1 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onReceive(g.id)}
                    loading={busy === g.id}
                    iconLeft={<Send className="h-3.5 w-3.5 text-green-600" />}
                    title="استلام — ينشئ حركات مخزون تلقائياً"
                  >
                    <span className="text-green-700 text-xs">استلام</span>
                  </Button>
                )}
                {/* Cancel: Draft (1) only */}
                {g.status === 1 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onCancel(g.id)}
                    loading={busy === g.id}
                    iconLeft={<X className="h-3.5 w-3.5 text-red-500" />}
                    title="إلغاء"
                  >
                    <span className="text-red-600 text-xs">إلغاء</span>
                  </Button>
                )}
              </div>
            ),
          },
        ]}
        data={grs}
        loading={loading}
        rowKey={(g) => g.id}
        rowHref={(g) => `/procurement/goods-receipts/${g.id}`}
        emptyMessage="لا توجد استلامات بضاعة بعد."
      />

      {!loading && grs.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">{grs.length} استلام</p>
      )}
    </div>
  );
}
