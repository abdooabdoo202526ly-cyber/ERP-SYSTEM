'use client';

// صفحة قائمة أوامر الشراء (Purchase Orders) — جدول مع actions + workflow buttons

import { useEffect, useState } from 'react';
import { formatDate } from '@/lib/utils';
import Link from 'next/link';
import {
  Plus,
  Eye,
  Pencil,
  Power,
  CheckCircle2,
  Send,
  X,
} from 'lucide-react';
import { Button, Table, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  PurchaseOrder,
  PO_STATUSES,
  PO_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

export default function PurchaseOrdersPage() {
  const { loading: authLoading } = useAuth();
  const [pos, setPOs] = useState<PurchaseOrder[]>([]);
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
      const data = await procurementApi.listPOs();
      setPOs(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل أوامر الشراء.'));
    } finally {
      setLoading(false);
    }
  };

  const onApprove = async (id: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm('هل تريد اعتماد أمر الشراء هذا؟')) return;
    setBusy(id);
    try {
      await procurementApi.approvePO(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الاعتماد.'));
    } finally {
      setBusy(null);
    }
  };

  const onSend = async (id: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm('هل تريد إرسال أمر الشراء للمورّد؟')) return;
    setBusy(id);
    try {
      await procurementApi.sendPO(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الإرسال.'));
    } finally {
      setBusy(null);
    }
  };

  const onCancel = async (id: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm('هل تريد إلغاء أمر الشراء هذا؟')) return;
    setBusy(id);
    try {
      await procurementApi.cancelPO(id);
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
        title="📄 أوامر الشراء"
        description="قائمة Purchase Orders (PO)"
        actions={
          <Link href="/procurement/purchase-orders/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              أمر شراء جديد
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
            key: 'poNumber',
            header: 'رقم الأمر',
            render: (p) => <span className="font-mono text-blue-600 font-semibold">{p.poNumber}</span>,
          },
          {
            key: 'vendor',
            header: 'المورّد',
            render: (p) => p.vendorName ? <span className="font-semibold text-gray-800">{p.vendorName}</span> : <span className="text-gray-400 text-xs font-mono">{p.vendorId?.substring(0, 8)}</span>,
          },
          {
            key: 'project',
            header: 'المشروع / مركز التكلفة',
            render: (p) => (
              <div className="text-xs space-y-0.5">
                {p.projectName ? <div className="font-semibold text-blue-700">📁 {p.projectName}</div> : <span className="text-gray-400">—</span>}
                {p.costCenterName ? <div className="text-gray-600">💼 {p.costCenterName}</div> : <span className="text-red-500">⚠️ بدون مركز</span>}
              </div>
            ),
          },
          {
            key: 'orderDate',
            header: 'تاريخ الطلب',
            render: (p) => (
              <span className="text-sm text-gray-700">
                {formatDate(p.orderDate)}
              </span>
            ),
          },
          {
            key: 'expectedDate',
            header: 'تاريخ التوصيل المتوقع',
            render: (p) =>
              p.expectedDate ? (
                <span className="text-sm text-gray-700">
                  {formatDate(p.expectedDate)}
                </span>
              ) : (
                <span className="text-gray-400 text-xs">—</span>
              ),
          },
          {
            key: 'lines',
            header: 'عدد البنود',
            align: 'center',
            render: (p) => <Badge variant="neutral">{p.lines?.length || 0}</Badge>,
          },
          {
            key: 'total',
            header: 'الإجمالي',
            align: 'end',
            render: (p) => (
              <div className="text-end">
                <p className="font-bold text-gray-800">
                  {p.totalAmount?.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </p>
                <p className="text-[10px] text-gray-500 font-mono">{p.currency}</p>
              </div>
            ),
          },
          {
            key: 'status',
            header: 'الحالة',
            render: (p) => (
              <Badge variant={PO_STATUS_VARIANTS[p.status] || 'neutral'}>
                {PO_STATUSES[p.status] || p.status}
              </Badge>
            ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (p) => (
              <div className="flex items-center gap-1 justify-center flex-wrap">
                <Link href={`/procurement/purchase-orders/${p.id}`}>
                  <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3.5 w-3.5" />}>
                    عرض
                  </Button>
                </Link>
                {/* Edit only for Draft (1) */}
                {p.status === 1 && (
                  <Link href={`/procurement/purchase-orders/${p.id}/edit`}>
                    <Button variant="ghost" size="sm" iconLeft={<Pencil className="h-3.5 w-3.5" />}>
                      تعديل
                    </Button>
                  </Link>
                )}
                {/* Approve: Draft (1) only */}
                {p.status === 1 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onApprove(p.id)}
                    loading={busy === p.id}
                    iconLeft={<CheckCircle2 className="h-3.5 w-3.5 text-blue-600" />}
                    title="اعتماد"
                  >
                    <span className="text-blue-600 text-xs">اعتماد</span>
                  </Button>
                )}
                {/* Send: Approved (3) only */}
                {p.status === 3 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onSend(p.id)}
                    loading={busy === p.id}
                    iconLeft={<Send className="h-3.5 w-3.5 text-green-600" />}
                    title="إرسال للمورّد"
                  >
                    <span className="text-green-700 text-xs">إرسال</span>
                  </Button>
                )}
                {/* Cancel: any non-cancelled non-received */}
                {p.status !== 5 && p.status !== 6 && (
                  <Button
                    variant="ghost"
                    size="sm"
                    onClick={() => onCancel(p.id)}
                    loading={busy === p.id}
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
        data={pos}
        loading={loading}
        rowKey={(p) => p.id}
        emptyMessage="لا توجد أوامر شراء. أنشئ أول أمر شراء."
      />

      {!loading && pos.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">{pos.length} أمر شراء</p>
      )}
    </div>
  );
}
