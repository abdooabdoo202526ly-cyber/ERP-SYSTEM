'use client';

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Package, Building2, User, FileText, Calendar, Hash, MapPin } from 'lucide-react';
import { Button, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { procurementApi, GoodsReceipt, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';

export default function GoodsReceiptDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params?.id as string;
  const { loading: authLoading } = useAuth();
  const [gr, setGR] = useState<GoodsReceipt | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading || !id) return;
    load();
  }, [authLoading, id]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await procurementApi.getGR(id);
      setGR(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل بيانات الاستلام.'));
    } finally {
      setLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="text-center py-12 text-gray-500">جاري التحميل...</div>
    );
  }

  if (error || !gr) {
    return (
      <div>
        <PageHeader title="استلام البضاعة" description="تفاصيل الاستلام" />
        <div className="bg-red-50 border border-red-200 rounded-lg p-4 text-red-700">
          {error || 'الاستلام غير موجود.'}
        </div>
        <Button onClick={() => router.back()} variant="secondary" className="mt-4">
          العودة
        </Button>
      </div>
    );
  }

  const statusLabel = (s: number) => {
    const map: Record<number, { label: string; variant: string }> = {
      1: { label: 'مسودة', variant: 'gray' },
      2: { label: 'مُستلَم', variant: 'green' },
      3: { label: 'مُلغى', variant: 'red' },
    };
    return map[s] ?? { label: 'غير معروف', variant: 'gray' };
  };
  const status = statusLabel(gr.status);

  return (
    <div>
      <PageHeader
        title={`📦 ${gr.grNumber}`}
        description={`استلام بضاعة — ${gr.poNumber ? `من أمر شراء ${gr.poNumber}` : 'بدون PO'}`}
        actions={
          <Button
            onClick={() => router.push('/procurement/goods-receipts')}
            variant="secondary"
            iconLeft={<ArrowRight className="h-4 w-4" />}
          >
            العودة للقائمة
          </Button>
        }
      />

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-4">
        {/* Header info */}
        <div className="bg-white rounded-xl shadow-sm p-4">
          <h3 className="text-sm font-semibold text-gray-500 mb-3 flex items-center gap-2">
            <Hash className="h-4 w-4" /> معلومات الاستلام
          </h3>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500">الرقم:</dt>
              <dd className="font-mono font-semibold">{gr.grNumber}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">الحالة:</dt>
              <dd><Badge variant={status.variant as any}>{status.label}</Badge></dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">التاريخ:</dt>
              <dd>{formatDate(gr.receivedDate)}</dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500">تاريخ الإنشاء:</dt>
              <dd className="text-xs">{formatDate(gr.createdAt)}</dd>
            </div>
          </dl>
        </div>

        {/* PO card */}
        <div className="bg-white rounded-xl shadow-sm p-4">
          <h3 className="text-sm font-semibold text-gray-500 mb-3 flex items-center gap-2">
            <FileText className="h-4 w-4" /> أمر الشراء
          </h3>
          {gr.poNumber ? (
            <dl className="space-y-2 text-sm">
              <div className="flex justify-between">
                <dt className="text-gray-500">رقم PO:</dt>
                <dd>
                  <Link href={`/procurement/purchase-orders/${gr.purchaseOrderId}`} className="font-mono font-semibold text-blue-600 hover:underline">
                    {gr.poNumber}
                  </Link>
                </dd>
              </div>
              {gr.poStatus && (
                <div className="flex justify-between">
                  <dt className="text-gray-500">حالة PO:</dt>
                  <dd><Badge variant="info">{gr.poStatus}</Badge></dd>
                </div>
              )}
            </dl>
          ) : (
            <p className="text-sm text-gray-400">— غير محدد —</p>
          )}
        </div>

        {/* Vendor + Warehouse */}
        <div className="bg-white rounded-xl shadow-sm p-4">
          <h3 className="text-sm font-semibold text-gray-500 mb-3 flex items-center gap-2">
            <Building2 className="h-4 w-4" /> المورّد والمخزن
          </h3>
          <dl className="space-y-2 text-sm">
            <div className="flex justify-between">
              <dt className="text-gray-500 flex items-center gap-1"><User className="h-3 w-3" /> المورّد:</dt>
              <dd className="font-semibold text-end">
                {gr.vendorName ?? <span className="text-gray-400">—</span>}
                {gr.vendorCode && <span className="text-xs text-gray-400 mr-1">({gr.vendorCode})</span>}
              </dd>
            </div>
            <div className="flex justify-between">
              <dt className="text-gray-500 flex items-center gap-1"><MapPin className="h-3 w-3" /> المخزن:</dt>
              <dd className="font-semibold text-end">
                {gr.warehouseName ?? <span className="text-gray-400">—</span>}
                {gr.warehouseCode && <span className="text-xs text-gray-400 mr-1">({gr.warehouseCode})</span>}
              </dd>
            </div>
          </dl>
        </div>
      </div>

      {/* Notes */}
      {gr.notes && (
        <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4 mb-4">
          <h3 className="text-sm font-semibold text-yellow-800 mb-1">📝 ملاحظات</h3>
          <p className="text-sm text-yellow-700">{gr.notes}</p>
        </div>
      )}

      {/* Lines */}
      <div className="bg-white rounded-xl shadow-sm p-4">
        <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
          <Package className="h-4 w-4" /> بنود الاستلام ({gr.lines?.length ?? 0})
        </h3>
        {!gr.lines || gr.lines.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-4">لا توجد بنود.</p>
        ) : (
          <table className="w-full text-sm">
            <thead className="bg-gray-50">
              <tr>
                <th className="px-3 py-2 text-start text-xs font-semibold text-gray-600">الصنف</th>
                <th className="px-3 py-2 text-start text-xs font-semibold text-gray-600">الكمية</th>
                <th className="px-3 py-2 text-start text-xs font-semibold text-gray-600">سعر الوحدة</th>
                <th className="px-3 py-2 text-start text-xs font-semibold text-gray-600">الإجمالي</th>
                <th className="px-3 py-2 text-start text-xs font-semibold text-gray-600">ملاحظات</th>
              </tr>
            </thead>
            <tbody>
              {gr.lines.map((l) => (
                <tr key={l.id} className="border-b border-gray-100 last:border-0">
                  <td className="px-3 py-2">{l.itemName ?? <span className="text-gray-400 font-mono text-xs">{l.itemId}</span>}</td>
                  <td className="px-3 py-2 font-mono">{l.quantity}</td>
                  <td className="px-3 py-2 font-mono">{(l as any).unitCost?.toFixed?.(2) ?? '—'}</td>
                  <td className="px-3 py-2 font-mono">{(l as any).subTotal?.toFixed?.(2) ?? '—'}</td>
                  <td className="px-3 py-2 text-gray-500 text-xs">{l.notes ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
