'use client';

// صفحة تفاصيل فاتورة المورّد (Vendor Bill Detail) — bill info + lines + summary
// Workflow: Post (if Draft) | Pay (if Posted, link to /payments/new?vendorBillId=...)
// Journal entry reference (if Posted) | Linked GR (if any)

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Send,
  Wallet,
  FileText,
  Building2,
  Calendar,
  Hash,
  Package,
  Receipt,
  CheckCircle2,
  Pencil,
} from 'lucide-react';
import { Button, Badge, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  VendorBill,
  BILL_STATUSES,
  BILL_STATUS_VARIANTS,
  GoodsReceipt,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function BillDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();
  const [bill, setBill] = useState<VendorBill | null>(null);
  const [gr, setGR] = useState<GoodsReceipt | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    if (!params?.id) return;
    setLoading(true);
    setError(null);
    try {
      const b = await procurementApi.getBill(params.id);
      setBill(b);
      // جلب الـ GR المرتبط (إن وُجد) لعرض رابط له
      if (b.goodsReceiptId) {
        try {
          const g = await procurementApi.getGR(b.goodsReceiptId);
          setGR(g);
        } catch {
          setGR(null);
        }
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الفاتورة.'));
    } finally {
      setLoading(false);
    }
  }, [params?.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onPost = async () => {
    if (!bill) return;
    if (typeof window === 'undefined') return;
    if (
      !window.confirm(
        `سيتم ترحيل الفاتورة ${bill.billNumber} وإنشاء قيد محاسبي. هل أنت متأكد؟`
      )
    )
      return;
    setActionLoading(true);
    try {
      const updated = await procurementApi.postBill(bill.id);
      setBill(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل ترحيل الفاتورة.'));
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="فاتورة مورّد" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (error || !bill) {
    return (
      <div>
        <PageHeader
          title="فاتورة مورّد"
          actions={
            <Link href="/procurement/bills">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'الفاتورة غير موجودة.'}
          </div>
        </Card>
      </div>
    );
  }

  // status codes: Draft=1, Posted=2, Paid=3, Cancelled=4
  const isDraft = bill.status === 1;
  const isPosted = bill.status === 2;
  const isPaid = bill.status === 3;
  const isCancelled = bill.status === 4;

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-orange-100 text-orange-700 flex items-center justify-center">
              <Receipt className="h-5 w-5" />
            </div>
            <div>
              <span className="font-mono">{bill.billNumber}</span>
              {bill.vendorName && (
                <p className="text-xs text-gray-500 font-normal mt-0.5">{bill.vendorName}</p>
              )}
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            <Badge variant={BILL_STATUS_VARIANTS[bill.status] || 'neutral'}>
              {BILL_STATUSES[bill.status] || bill.status}
            </Badge>
            <span className="text-gray-400">•</span>
            <span>العملة: {bill.currency}</span>
            <span className="text-gray-400">•</span>
            <span>{bill.lines.length} بند</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'فواتير الموردين', href: '/procurement/bills' },
          { label: bill.billNumber },
        ]}
        actions={
          <div className="flex items-center gap-2 flex-wrap">
            <Link href="/procurement/bills">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>

            {/* Edit — only for Draft */}
            {isDraft && (
              <Link href={`/procurement/bills/${bill.id}/edit`}>
                <Button variant="outline" iconLeft={<Pencil className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}

            {/* Post — only for Draft */}
            {isDraft && (
              <Button
                variant="primary"
                onClick={onPost}
                loading={actionLoading}
                iconLeft={<Send className="h-4 w-4" />}
              >
                ترحيل الفاتورة
              </Button>
            )}

            {/* Pay — only for Posted (not Paid) */}
            {isPosted && (
              <Link href={`/payments/new?vendorBillId=${bill.id}`}>
                <Button variant="primary" iconLeft={<Wallet className="h-4 w-4" />}>
                  دفع الفاتورة
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
        {/* معلومات الفاتورة */}
        <Card title="📋 معلومات الفاتورة" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Hash className="h-3 w-3" /> رقم الفاتورة
              </p>
              <p className="font-mono font-semibold text-blue-700">{bill.billNumber}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Building2 className="h-3 w-3" /> المورّد
              </p>
              {bill.vendorName ? (
                <Link
                  href={`/procurement/vendors/${bill.vendorId}`}
                  className="text-blue-600 hover:underline font-semibold"
                >
                  {bill.vendorName}
                </Link>
              ) : (
                <span className="text-gray-400 font-mono text-xs">{bill.vendorId}</span>
              )}
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Calendar className="h-3 w-3" /> تاريخ الفاتورة
              </p>
              <p className="font-semibold">{formatDate(bill.billDate)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Calendar className="h-3 w-3" /> تاريخ الاستحقاق
              </p>
              <p className="font-semibold">{formatDate(bill.dueDate)}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Package className="h-3 w-3" /> سند الاستلام
              </p>
              {gr ? (
                <Link
                  href={`/procurement/goods-receipts/${gr.id}`}
                  className="text-blue-600 hover:underline font-mono"
                >
                  {gr.grNumber}
                </Link>
              ) : bill.grNumber ? (
                <span className="font-mono">{bill.grNumber}</span>
              ) : (
                <span className="text-gray-400 font-mono text-xs">{bill.goodsReceiptId}</span>
              )}
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              <Badge variant={BILL_STATUS_VARIANTS[bill.status] || 'neutral'}>
                {BILL_STATUSES[bill.status] || bill.status}
              </Badge>
            </div>
            <FieldRow
              icon={<Calendar className="h-3 w-3" />}
              label="تاريخ الإنشاء"
              value={formatDateTime(bill.createdAt)}
            />
            <div>
              <p className="text-xs text-gray-500 mb-1">المعرّف</p>
              <p className="text-xs font-mono text-gray-700" dir="ltr">
                {bill.id.substring(0, 8)}…
              </p>
            </div>
          </div>

          {/* Journal entry reference if posted */}
          {isPosted && (
            <div className="mt-4 pt-4 border-t">
              <p className="text-xs text-gray-500 mb-2">📒 القيد المحاسبي</p>
              {bill.journalEntryId ? (
                <Link
                  href={`/finance/journal-entries/${bill.journalEntryId}`}
                  className="inline-flex items-center gap-2 px-3 py-2 rounded-lg bg-blue-50 border border-blue-200 hover:bg-blue-100 text-sm"
                >
                  <FileText className="h-4 w-4 text-blue-600" />
                  <span className="font-mono text-xs text-gray-700" dir="ltr">
                    {bill.journalEntryId}
                  </span>
                  <span className="text-blue-700">— عرض القيد</span>
                </Link>
              ) : (
                <p className="text-sm text-gray-400">— لم يُسجَّل القيد بعد —</p>
              )}
            </div>
          )}

          {isPaid && (
            <div className="mt-4 pt-4 border-t bg-green-50 -mx-4 px-4 py-3 rounded">
              <div className="flex items-center gap-2 text-green-700 text-sm">
                <CheckCircle2 className="h-4 w-4" />
                <span className="font-semibold">تم سداد هذه الفاتورة بالكامل.</span>
              </div>
            </div>
          )}

          {bill.notes && (
            <div className="mt-4 pt-4 border-t">
              <p className="text-xs text-gray-500 mb-1">📝 ملاحظات</p>
              <p className="text-sm text-gray-800 whitespace-pre-wrap">{bill.notes}</p>
            </div>
          )}
        </Card>

        {/* الملخص المالي */}
        <Card title="💰 الملخص المالي">
          <div className="space-y-3 text-sm">
            <div className="flex justify-between">
              <span className="text-gray-600">المجموع الفرعي:</span>
              <span className="font-mono font-semibold">
                {formatNumber(bill.subTotal)} {bill.currency}
              </span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">الضريبة:</span>
              <span className="font-mono font-semibold">
                {formatNumber(bill.taxAmount)} {bill.currency}
              </span>
            </div>
            <div className="flex justify-between border-t pt-2 bg-blue-50 -mx-4 px-4 py-2 mt-2">
              <span className="font-bold text-blue-900">الإجمالي:</span>
              <span className="font-mono font-bold text-blue-900 text-lg">
                {formatNumber(bill.totalAmount)} {bill.currency}
              </span>
            </div>
            {isPaid && (
              <div className="flex justify-between pt-2 border-t bg-green-50 -mx-4 px-4 py-2 mt-2">
                <span className="font-bold text-green-800">المدفوع:</span>
                <span className="font-mono font-bold text-green-800">
                  {formatNumber(bill.totalAmount)} {bill.currency}
                </span>
              </div>
            )}
            {isPosted && (
              <div className="flex justify-between pt-2 border-t bg-amber-50 -mx-4 px-4 py-2 mt-2">
                <span className="font-bold text-amber-800">المستحق:</span>
                <span className="font-mono font-bold text-amber-800">
                  {formatNumber(bill.totalAmount)} {bill.currency}
                </span>
              </div>
            )}
          </div>
        </Card>
      </div>

      {/* بنود الفاتورة */}
      <Card
        className="mt-4"
        title={
          <span>
            <FileText className="h-4 w-4 inline-block me-2" />
            بنود الفاتورة
            {bill.lines.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({bill.lines.length})
              </span>
            )}
          </span>
        }
      >
        {!bill.lines || bill.lines.length === 0 ? (
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
                {bill.lines.map((l, idx) => {
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

      {isCancelled && (
        <div className="mt-4 bg-red-50 border border-red-200 rounded-xl p-3 text-sm text-red-700">
          ⚠️ هذه الفاتورة ملغاة ولا يمكن تنفيذ أي إجراء عليها.
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
