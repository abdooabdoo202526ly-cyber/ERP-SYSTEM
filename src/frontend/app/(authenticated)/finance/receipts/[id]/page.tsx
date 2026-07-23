'use client';

// صفحة تفاصيل سند القبض (Receipt Detail) — يعرض معلومات السند
// + التخصيصات على الفواتير + أزرار طباعة / تعديل / ترحيل / عكس

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Edit,
  Printer,
  Send,
  RotateCcw,
  Receipt as ReceiptIcon,
  User as UserIcon,
  Calendar,
  Hash,
  FileText,
  CreditCard,
  CheckCircle2,
  Wallet,
} from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  receiptsApi,
  Receipt,
  PAYMENT_METHODS,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

// ============ Helpers ============

function FieldRow({
  icon,
  label,
  value,
  mono,
}: {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
  mono?: boolean;
}) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
        {icon} {label}
      </p>
      <p className={`text-sm break-words ${mono ? 'font-mono' : ''}`}>
        {value || <span className="text-gray-400">—</span>}
      </p>
    </div>
  );
}

// ============ Page ============

export default function ReceiptDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const id = params?.id;

  const [receipt, setReceipt] = useState<Receipt | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      const r = await receiptsApi.get(id);
      setReceipt(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل سند القبض.'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const onPost = async () => {
    if (!receipt) return;
    if (typeof window === 'undefined') return;
    if (
      !window.confirm(
        'سيتم ترحيل سند القبض وإنشاء قيد محاسبي (Dr 1210 / Cr 1230). هل أنت متأكد؟'
      )
    )
      return;
    setActionLoading(true);
    try {
      const updated = await receiptsApi.post(receipt.id);
      setReceipt(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل ترحيل السند.'));
    } finally {
      setActionLoading(false);
    }
  };

  const onReverse = async () => {
    if (!receipt) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm('سيتم عكس السند وإنشاء قيد عكسي. هل أنت متأكد؟')) return;
    setActionLoading(true);
    try {
      const updated = await receiptsApi.reverse(receipt.id);
      setReceipt(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل عكس السند.'));
    } finally {
      setActionLoading(false);
    }
  };

  const onPrint = () => {
    if (typeof window === 'undefined') return;
    // v1.0.32: use dedicated print page
    window.open(`/finance/receipts/${id}/print`, '_blank');
  };

  // ============ Render states ============

  if (loading) {
    return (
      <div>
        <PageHeader title="سند قبض" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!receipt) {
    return (
      <div>
        <PageHeader
          title="سند قبض"
          actions={
            <Link href="/finance/receipts">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'سند القبض غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/finance/receipts">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  const isDraft = !receipt.postedAt;
  const isReversed = !!receipt.notes && receipt.notes.includes('[REVERSED]'); // BE قد يضع علامة
  const isEditable = isDraft; // يمكن تعديل المسودة فقط
  const paymentLabel = receipt.paymentMethod
    ? PAYMENT_METHODS[receipt.paymentMethod] || receipt.paymentMethod
    : null;

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div
              className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                isDraft ? 'bg-yellow-100 text-yellow-700' : 'bg-green-100 text-green-700'
              }`}
            >
              <ReceiptIcon className="h-5 w-5" />
            </div>
            <div>
              <span>سند قبض {receipt.receiptNumber}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {receipt.receiptNumber}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {isDraft ? (
              <Badge variant="warning">مسودة</Badge>
            ) : (
              <Badge variant="success">مُرحّل</Badge>
            )}
            {isReversed && <Badge variant="danger">معكوس</Badge>}
            <span className="text-gray-400">•</span>
            <span>العميل: {receipt.customerName || '—'}</span>
            <span className="text-gray-400">•</span>
            <span>المبلغ: {formatNumber(receipt.amount)} {receipt.currencyCode}</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/receipts' },
          { label: 'سندات القبض', href: '/finance/receipts' },
          { label: receipt.receiptNumber },
        ]}
        actions={
          <div className="flex items-center gap-2 no-print">
            <Link href="/finance/receipts">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            <Button
              variant="ghost"
              onClick={onPrint}
              iconLeft={<Printer className="h-4 w-4" />}
            >
              طباعة
            </Button>
            {isEditable && (
              <Link href={`/finance/receipts/${receipt.id}/edit`}>
                <Button variant="primary" iconLeft={<Edit className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}
            {isDraft && (
              <Button
                variant="primary"
                loading={actionLoading}
                onClick={onPost}
                iconLeft={<Send className="h-4 w-4" />}
              >
                ترحيل
              </Button>
            )}
            {!isDraft && !isReversed && (
              <Button
                variant="danger"
                loading={actionLoading}
                onClick={onReverse}
                iconLeft={<RotateCcw className="h-4 w-4" />}
              >
                عكس
              </Button>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm no-print">
          {error}
        </div>
      )}

      {/* Print header (يظهر فقط عند الطباعة) */}
      <div className="hidden print:block mb-6">
        <h1 className="text-2xl font-bold text-center">سند قبض</h1>
        <p className="text-center text-sm text-gray-600">
          رقم السند: <span className="font-mono font-semibold">{receipt.receiptNumber}</span>
        </p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* معلومات السند */}
        <Card title="📋 معلومات السند" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <FieldRow
              icon={<Hash className="h-4 w-4" />}
              label="رقم السند"
              value={receipt.receiptNumber}
              mono
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ السند"
              value={formatDate(receipt.receiptDate)}
            />
            <FieldRow
              icon={<UserIcon className="h-4 w-4" />}
              label="العميل"
              value={
                <Link
                  href={`/finance/customers/${receipt.customerId}`}
                  className="text-blue-600 hover:underline font-semibold"
                >
                  {receipt.customerName || receipt.customerId}
                </Link>
              }
            />
            <FieldRow
              icon={<Wallet className="h-4 w-4" />}
              label="المبلغ"
              value={
                <span className="font-mono font-bold text-lg text-green-700">
                  {formatNumber(receipt.amount)} {receipt.currencyCode}
                </span>
              }
            />
            <FieldRow
              icon={<CreditCard className="h-4 w-4" />}
              label="طريقة الدفع"
              value={paymentLabel ? <Badge variant="info">{paymentLabel}</Badge> : null}
            />
            <FieldRow
              icon={<CheckCircle2 className="h-4 w-4" />}
              label="الحالة"
              value={
                isDraft ? (
                  <Badge variant="warning">مسودة</Badge>
                ) : isReversed ? (
                  <Badge variant="danger">معكوس</Badge>
                ) : (
                  <Badge variant="success">مُرحّل</Badge>
                )
              }
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ الإنشاء"
              value={formatDateTime(receipt.createdAt)}
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ الترحيل"
              value={formatDateTime(receipt.postedAt)}
            />
            <FieldRow
              icon={<Hash className="h-4 w-4" />}
              label="القيد المحاسبي"
              value={
                receipt.journalEntryId ? (
                  <span className="font-mono text-xs">
                    {receipt.journalEntryId.slice(0, 8)}…{receipt.journalEntryId.slice(-4)}
                  </span>
                ) : null
              }
              mono
            />
            <div className="md:col-span-2">
              <FieldRow
                icon={<FileText className="h-4 w-4" />}
                label="ملاحظات"
                value={receipt.notes}
              />
            </div>
          </div>
        </Card>

        {/* ملخص */}
        <Card title="💵 ملخص">
          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 rounded-lg bg-green-50">
              <span className="text-sm text-gray-600">المبلغ الإجمالي</span>
              <span className="font-mono font-bold text-green-700 text-lg">
                {formatNumber(receipt.amount)}
              </span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">عدد التخصيصات</span>
              <span className="text-2xl font-bold text-gray-700">
                {receipt.allocations.length}
              </span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">العملة</span>
              <span className="font-mono font-semibold">{receipt.currencyCode}</span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">طريقة الدفع</span>
              <span className="text-sm font-semibold">
                {paymentLabel || <span className="text-gray-400">—</span>}
              </span>
            </div>
          </div>
        </Card>
      </div>

      {/* التخصيصات على الفواتير */}
      <Card
        className="mt-4"
        title={
          <span>
            🔗 التخصيصات على الفواتير
            {receipt.allocations.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({receipt.allocations.length})
              </span>
            )}
          </span>
        }
      >
        {receipt.allocations.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <FileText className="h-10 w-10 mx-auto text-gray-300 mb-2" />
            <p className="text-sm">لا توجد تخصيصات على فواتير.</p>
            <p className="text-xs mt-1 text-gray-400">
              السند قد يكون دفعة مقدمة — أضف تخصيصات عبر التعديل إن كان لا يزال مسودة.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500 border-b">
                  <th className="py-2 pr-2">#</th>
                  <th className="py-2 pr-2">رقم الفاتورة</th>
                  <th className="py-2 pr-2 text-left">المبلغ المخصص</th>
                  <th className="py-2 pr-2 text-left">النسبة</th>
                </tr>
              </thead>
              <tbody>
                {receipt.allocations.map((a, idx) => {
                  const ratio =
                    receipt.amount > 0 ? (a.amountApplied / receipt.amount) * 100 : 0;
                  return (
                    <tr key={a.id} className="border-b hover:bg-gray-50">
                      <td className="py-2 pr-2 text-gray-500">{idx + 1}</td>
                      <td className="py-2 pr-2">
                        {a.salesInvoiceId ? (
                          <Link
                            href={`/finance/sales-invoices/${a.salesInvoiceId}`}
                            className="font-mono font-semibold text-blue-600 hover:underline"
                          >
                            {a.salesInvoiceNumber ||
                              `${a.salesInvoiceId.slice(0, 8)}…${a.salesInvoiceId.slice(-4)}`}
                          </Link>
                        ) : (
                          <span className="text-gray-400">—</span>
                        )}
                      </td>
                      <td className="py-2 pr-2 text-left font-mono font-semibold">
                        {formatNumber(a.amountApplied)} {receipt.currencyCode}
                      </td>
                      <td className="py-2 pr-2 text-left font-mono text-gray-600">
                        {ratio.toFixed(2)}%
                      </td>
                    </tr>
                  );
                })}
              </tbody>
              <tfoot>
                <tr className="border-t-2 border-gray-300 font-semibold bg-gray-50">
                  <td className="py-2 pr-2" colSpan={2}>
                    الإجمالي
                  </td>
                  <td className="py-2 pr-2 text-left font-mono">
                    {formatNumber(
                      receipt.allocations.reduce((s, a) => s + a.amountApplied, 0)
                    )}{' '}
                    {receipt.currencyCode}
                  </td>
                  <td className="py-2 pr-2 text-left font-mono">100%</td>
                </tr>
              </tfoot>
            </table>
          </div>
        )}
      </Card>

      {/* Print-only footer (signature lines) */}
      <div className="hidden print:block mt-12">
        <div className="grid grid-cols-2 gap-12 text-sm">
          <div>
            <p className="border-t border-gray-400 pt-2 mt-12">توقيع المستلم</p>
          </div>
          <div>
            <p className="border-t border-gray-400 pt-2 mt-12">توقيع المسؤول</p>
          </div>
        </div>
        <p className="text-center text-xs text-gray-500 mt-8">
          طُبع في {new Date().toLocaleString('en-GB')}
        </p>
      </div>

      {/* Print-friendly CSS — يخفي أزرار وعناصر الواجهة عند الطباعة */}
      <style jsx global>{`
        @media print {
          .no-print { display: none !important; }
          body { background: white !important; }
          aside, header, nav, .sidebar, [data-no-print] { display: none !important; }
          main, .print-area { padding: 0 !important; margin: 0 !important; }
        }
      `}</style>
    </div>
  );
}
