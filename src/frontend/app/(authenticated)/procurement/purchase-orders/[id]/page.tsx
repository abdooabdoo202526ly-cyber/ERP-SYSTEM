'use client';

// صفحة تفاصيل أمر الشراء (Purchase Order Detail) — full PO view
// Header: poNumber, vendor, dates, status
// Lines: item, qty, unitPrice, tax, total
// Subtotal/Tax/Total
// Workflow buttons (gated by status): Approve | Send | CreateGR | Cancel

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  CheckCircle2,
  Send,
  Package,
  XCircle,
  Pencil,
  FileText,
  Building2,
  Calendar,
  Hash,
  ShoppingCart,
} from 'lucide-react';
import { Button, Badge, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  PurchaseOrder,
  PO_STATUSES,
  PO_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function PurchaseOrderDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();
  const [po, setPO] = useState<PurchaseOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    if (!params?.id) return;
    setLoading(true);
    setError(null);
    try {
      const data = await procurementApi.getPO(params.id);
      setPO(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل أمر الشراء.'));
    } finally {
      setLoading(false);
    }
  }, [params?.id]);

  useEffect(() => {
    load();
  }, [load]);

  // ===== Workflow actions =====

  const onApprove = async () => {
    if (!po) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد الموافقة على الأمر ${po.poNumber}؟`)) return;
    setActionLoading(true);
    try {
      const updated = await procurementApi.approvePO(po.id);
      setPO(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل اعتماد الأمر.'));
    } finally {
      setActionLoading(false);
    }
  };

  const onSend = async () => {
    if (!po) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إرسال الأمر ${po.poNumber} إلى المورّد؟`)) return;
    setActionLoading(true);
    try {
      const updated = await procurementApi.sendPO(po.id);
      setPO(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إرسال الأمر.'));
    } finally {
      setActionLoading(false);
    }
  };

  // ===== Render =====

  if (loading) {
    return (
      <div>
        <PageHeader title="أمر شراء" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (error || !po) {
    return (
      <div>
        <PageHeader
          title="أمر شراء"
          actions={
            <Link href="/procurement/purchase-orders">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'أمر الشراء غير موجود.'}
          </div>
        </Card>
      </div>
    );
  }

  // status codes: Draft=1, Pending=2, Approved=3, Sent=4, Received=5, Cancelled=6
  const isDraft = po.status === 1;
  const isPending = po.status === 2;
  const isApproved = po.status === 3;
  const isSent = po.status === 4;
  const isCancelled = po.status === 6;
  const isClosed = po.status === 5 || isCancelled;

  // حساب المجاميع من الـ lines (fallback لو الـ backend لم يرسل subTotal/taxAmount)
  const computedSubTotal = po.lines.reduce(
    (sum, l) => sum + (l.subTotal ?? l.quantity * l.unitPrice),
    0
  );
  const computedTax = po.lines.reduce(
    (sum, l) => sum + (l.subTotal ?? l.quantity * l.unitPrice) * ((l.taxRate || 0) / 100),
    0
  );
  const computedTotal = computedSubTotal + computedTax;
  const subTotal = computedSubTotal;
  const taxAmount = computedTax;
  const totalAmount = computedTotal;

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-blue-100 text-blue-700 flex items-center justify-center">
              <ShoppingCart className="h-5 w-5" />
            </div>
            <div>
              <span className="font-mono">{po.poNumber}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {po.vendorName || '—'}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            <Badge variant={PO_STATUS_VARIANTS[po.status] || 'neutral'}>
              {PO_STATUSES[po.status] || po.status}
            </Badge>
            <span className="text-gray-400">•</span>
            <span>العملة: {po.currency}</span>
            <span className="text-gray-400">•</span>
            <span>{po.lines.length} بند</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'أوامر الشراء', href: '/procurement/purchase-orders' },
          { label: po.poNumber },
        ]}
        actions={
          <div className="flex items-center gap-2 flex-wrap">
            <Link href="/procurement/purchase-orders">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>

            {/* Edit — only for Draft */}
            {isDraft && (
              <Link href={`/procurement/purchase-orders/${po.id}/edit`}>
                <Button variant="outline" iconLeft={<Pencil className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}

            {/* Approve — Draft or Pending */}
            {(isDraft || isPending) && (
              <Button
                variant="primary"
                onClick={onApprove}
                loading={actionLoading}
                iconLeft={<CheckCircle2 className="h-4 w-4" />}
              >
                اعتماد
              </Button>
            )}

            {/* Send to Vendor — only Approved */}
            {isApproved && (
              <Button
                variant="primary"
                onClick={onSend}
                loading={actionLoading}
                iconLeft={<Send className="h-4 w-4" />}
              >
                إرسال للمورّد
              </Button>
            )}

            {/* Create Goods Receipt — only Sent */}
            {isSent && (
              <Link href={`/procurement/goods-receipts/new?poId=${po.id}`}>
                <Button variant="primary" iconLeft={<Package className="h-4 w-4" />}>
                  إنشاء استلام بضاعة
                </Button>
              </Link>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* معلومات الأمر */}
        <Card title="📋 معلومات الأمر" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Hash className="h-3 w-3" /> رقم الأمر
              </p>
              <p className="font-mono font-semibold text-blue-700">{po.poNumber}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Building2 className="h-3 w-3" /> المورّد
              </p>
              {po.vendorName ? (
                <Link
                  href={`/procurement/vendors/${po.vendorId}`}
                  className="text-blue-600 hover:underline font-semibold"
                >
                  {po.vendorName}
                </Link>
              ) : (
                <span className="text-gray-400 font-mono text-xs">{po.vendorId}</span>
              )}
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Calendar className="h-3 w-3" /> تاريخ الطلب
              </p>
              <p className="font-semibold">{formatDate(po.orderDate)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Calendar className="h-3 w-3" /> تاريخ التوصيل المتوقع
              </p>
              <p className="font-semibold">{formatDate(po.expectedDate)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">العملة</p>
              <p className="font-mono font-semibold">{po.currency}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              <Badge variant={PO_STATUS_VARIANTS[po.status] || 'neutral'}>
                {PO_STATUSES[po.status] || po.status}
              </Badge>
            </div>
            <FieldRow
              icon={<Calendar className="h-3 w-3" />}
              label="تاريخ الإنشاء"
              value={formatDateTime(po.createdAt)}
            />
            <div>
              <p className="text-xs text-gray-500 mb-1">المعرّف</p>
              <p className="text-xs font-mono text-gray-700" dir="ltr">
                {po.id.substring(0, 8)}…
              </p>
            </div>
          </div>

          {po.notes && (
            <div className="mt-4 pt-4 border-t">
              <p className="text-xs text-gray-500 mb-1">📝 ملاحظات</p>
              <p className="text-sm text-gray-800 whitespace-pre-wrap">{po.notes}</p>
            </div>
          )}
        </Card>

        {/* الملخص المالي */}
        <Card title="💰 الملخص المالي">
          <div className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-600">المجموع الفرعي:</span>
              <span className="font-mono font-semibold">
                {formatNumber(subTotal)} {po.currency}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">الضريبة:</span>
              <span className="font-mono font-semibold">
                {formatNumber(taxAmount)} {po.currency}
              </span>
            </div>
            <div className="flex justify-between border-t pt-2 bg-blue-50 -mx-4 px-4 py-2 mt-2">
              <span className="font-bold text-blue-900">الإجمالي:</span>
              <span className="font-mono font-bold text-blue-900 text-lg">
                {formatNumber(totalAmount)} {po.currency}
              </span>
            </div>
            <div className="flex justify-between pt-2 border-t">
              <span className="text-gray-500 text-xs">المحسوب من البنود</span>
              <span className="text-xs text-gray-500 font-mono">
                {po.lines.length} × بند
              </span>
            </div>
          </div>
        </Card>
      </div>

      {/* بنود الأمر */}
      <Card
        className="mt-4"
        title={
          <span>
            <FileText className="h-4 w-4 inline-block me-2" />
            بنود أمر الشراء
            {po.lines.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({po.lines.length})
              </span>
            )}
          </span>
        }
      >
        {!po.lines || po.lines.length === 0 ? (
          <p className="text-sm text-gray-400 text-center py-4">لا توجد بنود.</p>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500 border-b bg-gray-50">
                  <th className="py-2 pr-2">#</th>
                  <th className="py-2 pr-2">الصنف</th>
                  <th className="py-2 pr-2 text-left">الكمية</th>
                  <th className="py-2 pr-2 text-left">سعر الوحدة</th>
                  <th className="py-2 pr-2 text-left">الضريبة %</th>
                  <th className="py-2 pr-2 text-left">المجموع</th>
                </tr>
              </thead>
              <tbody>
                {po.lines.map((l, idx) => {
                  const lineSub = l.subTotal ?? l.quantity * l.unitPrice;
                  const lineTax = lineSub * ((l.taxRate || 0) / 100);
                  const lineTotal = lineSub + lineTax;
                  return (
                    <tr key={l.id || idx} className="border-b">
                      <td className="py-2 pr-2 text-gray-500">{idx + 1}</td>
                      <td className="py-2 pr-2">
                        {l.itemName || (
                          <span className="text-gray-400 font-mono text-xs">{l.itemId}</span>
                        )}
                      </td>
                      <td className="py-2 pr-2 text-left font-mono">{formatNumber(l.quantity)}</td>
                      <td className="py-2 pr-2 text-left font-mono">{formatNumber(l.unitPrice)}</td>
                      <td className="py-2 pr-2 text-left font-mono">
                        {formatNumber(l.taxRate)}%
                      </td>
                      <td className="py-2 pr-2 text-left font-mono font-semibold">
                        {formatNumber(lineTotal)}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Card>

      {/* Workflow hint for closed states */}
      {isClosed && (
        <div className="mt-4 bg-gray-50 border border-gray-200 rounded-xl p-3 text-sm text-gray-600 flex items-center gap-2">
          <XCircle className="h-4 w-4 text-gray-400" />
          {isCancelled
            ? 'هذا الأمر ملغى. لا يمكن تنفيذ أي إجراء عليه.'
            : 'تم استلام هذا الأمر بالكامل. لا يمكن تعديله.'}
        </div>
      )}
    </div>
  );
}

// ============ Local subcomponents ============

interface FieldRowProps {
  icon: React.ReactNode;
  label: string;
  value?: string | null;
  mono?: boolean;
}

function FieldRow({ icon, label, value, mono }: FieldRowProps) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
        {icon} {label}
      </p>
      <p className={`text-sm ${mono ? 'font-mono' : ''} ${!value ? 'text-gray-400' : ''}`}>
        {value || '—'}
      </p>
    </div>
  );
}
