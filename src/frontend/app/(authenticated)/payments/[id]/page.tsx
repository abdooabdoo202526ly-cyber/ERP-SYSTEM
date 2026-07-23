'use client';

// صفحة تفاصيل دفعة — معلومات + allocations + أزرار Post/Allocate

import { useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import { ArrowRight, Send, Plus, FileText, CreditCard } from 'lucide-react';
import { Button, Badge, Card, PageHeader, Input, Select, Modal } from '@/components/ui';
import {
  paymentsApi,
  procurementApi,
  Payment,
  VendorBill,
  PAYMENT_STATUSES,
  PAYMENT_STATUS_VARIANTS,
  PAYMENT_METHODS,
  PAYMENT_PARTY_TYPES,
  PAYMENT_REF_TYPES,
  getErrorMessage,
} from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function PaymentDetailPage({ params }: { params: { id: string } }) {
  const { id } = params;
  const [payment, setPayment] = useState<Payment | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  // Allocate modal state
  const [allocModalOpen, setAllocModalOpen] = useState(false);
  const [bills, setBills] = useState<VendorBill[]>([]);
  const [billsLoading, setBillsLoading] = useState(false);
  const [allocRows, setAllocRows] = useState<{ refId: string; amountApplied: string }[]>([
    { refId: '', amountApplied: '' },
  ]);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await paymentsApi.get(id);
      setPayment(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الدفعة.'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const onPost = async () => {
    if (!payment) return;
    if (!confirm('سيتم ترحيل الدفعة وإنشاء قيد محاسبي. هل أنت متأكد؟')) return;
    setActionLoading(true);
    try {
      const updated = await paymentsApi.post(payment.id);
      setPayment(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل ترحيل الدفعة.'));
    } finally {
      setActionLoading(false);
    }
  };

  const openAllocateModal = async () => {
    setAllocModalOpen(true);
    setAllocRows([{ refId: '', amountApplied: '' }]);
    setBillsLoading(true);
    try {
      const data = await procurementApi.listBills();
      // فقط الفواتير المُرحَّلة وغير المدفوعة بالكامل
      setBills(data.filter((b) => b.status === 2 || b.status === 3));
    } catch (e: unknown) {
      alert(getErrorMessage(e, 'تعذّر تحميل فواتير الموردين.'));
    } finally {
      setBillsLoading(false);
    }
  };

  const addAllocRow = () => setAllocRows((r) => [...r, { refId: '', amountApplied: '' }]);
  const removeAllocRow = (i: number) =>
    setAllocRows((r) => (r.length > 1 ? r.filter((_, idx) => idx !== i) : r));
  const updateAllocRow = (i: number, patch: Partial<{ refId: string; amountApplied: string }>) => {
    setAllocRows((r) => r.map((row, idx) => (idx === i ? { ...row, ...patch } : row)));
  };

  const totalAllocInput = allocRows.reduce(
    (s, r) => s + (Number(r.amountApplied) || 0),
    0
  );

  const submitAllocations = async () => {
    if (!payment) return;
    const valid = allocRows.filter((r) => r.refId && Number(r.amountApplied) > 0);
    if (valid.length === 0) {
      alert('الرجاء إدخال تخصيص واحد على الأقل بمبلغ موجب.');
      return;
    }
    if (totalAllocInput > payment.onAccountAmount + 0.0001) {
      alert(
        `مجموع التخصيصات (${formatNumber(totalAllocInput)}) أكبر من المبلغ غير المخصص (${formatNumber(payment.onAccountAmount)}).`
      );
      return;
    }
    setActionLoading(true);
    try {
      const updated = await paymentsApi.allocate(payment.id, {
        allocations: valid.map((r) => ({
          refType: 'VendorBill' as const,
          refId: r.refId,
          amountApplied: Number(r.amountApplied),
        })),
      });
      setPayment(updated);
      setAllocModalOpen(false);
    } catch (e: unknown) {
      alert(getErrorMessage(e, 'فشل إضافة التخصيصات.'));
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="دفعة" description="جاري التحميل..." />
      </div>
    );
  }
  if (error && !payment) {
    return (
      <div>
        <PageHeader title="دفعة" description="خطأ" />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error}</div>
      </div>
    );
  }
  if (!payment) return null;

  const statusLabel = PAYMENT_STATUSES[payment.status] || '—';
  const statusVariant = PAYMENT_STATUS_VARIANTS[payment.status] || 'neutral';
  const isDraft = payment.status === 1;
  const isPosted = payment.status === 2;
  const canAllocate = isPosted && payment.onAccountAmount > 0;

  const billOptions = bills.map((b) => ({
    value: b.id,
    label: `${b.billNumber} — ${b.vendorName || 'مورّد'} (${formatNumber(b.totalAmount)} ${b.currency})`,
  }));

  return (
    <div>
      <PageHeader
        title={`💳 دفعة ${payment.paymentNumber}`}
        description={`${PAYMENT_PARTY_TYPES[payment.partyType] || payment.partyType} • ${formatDate(payment.paymentDate)}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المدفوعات', href: '/payments' },
          { label: payment.paymentNumber },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/payments">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
            {isDraft && (
              <Button variant="primary" loading={actionLoading} onClick={onPost} iconLeft={<Send className="h-4 w-4" />}>
                ترحيل الدفعة
              </Button>
            )}
            {canAllocate && (
              <Button variant="secondary" loading={actionLoading} onClick={openAllocateModal} iconLeft={<Plus className="h-4 w-4" />}>
                تخصيص
              </Button>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2">
          <div className="flex items-center justify-between mb-3">
            <h3 className="font-bold text-gray-800">معلومات الدفعة</h3>
            <Badge variant={statusVariant}>{statusLabel}</Badge>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            <div>
              <p className="text-gray-500">رقم الدفعة</p>
              <p className="font-mono font-semibold text-blue-600">{payment.paymentNumber}</p>
            </div>
            <div>
              <p className="text-gray-500">التاريخ</p>
              <p className="font-semibold">{formatDate(payment.paymentDate)}</p>
            </div>
            <div>
              <p className="text-gray-500">نوع الطرف</p>
              <Badge variant={payment.partyType === 'Vendor' ? 'warning' : 'info'}>
                {PAYMENT_PARTY_TYPES[payment.partyType] || payment.partyType}
              </Badge>
            </div>
            <div>
              <p className="text-gray-500">معرّف الطرف</p>
              <p className="font-mono text-xs">{payment.partyId.slice(0, 8)}...</p>
            </div>
            <div>
              <p className="text-gray-500">العملة</p>
              <p className="font-mono font-semibold">{payment.currencyCode}</p>
            </div>
            <div>
              <p className="text-gray-500">طريقة الدفع</p>
              <p className="font-semibold">{PAYMENT_METHODS[payment.paymentMethod] || payment.paymentMethod}</p>
            </div>
            <div>
              <p className="text-gray-500">تاريخ الترحيل</p>
              <p className="font-semibold">{formatDate(payment.postedAt)}</p>
            </div>
            <div>
              <p className="text-gray-500">القيد</p>
              <p className="font-mono text-xs">{payment.journalEntryId ? payment.journalEntryId.slice(0, 8) + '...' : '—'}</p>
            </div>
          </div>

          {payment.notes && (
            <div className="mt-4 p-3 bg-gray-50 rounded-lg text-sm">
              <p className="text-gray-500 mb-1">ملاحظات:</p>
              <p className="text-gray-800 whitespace-pre-wrap">{payment.notes}</p>
            </div>
          )}

          <h3 className="font-bold text-gray-800 pt-4 mt-4 border-t flex items-center gap-2">
            <CreditCard className="h-4 w-4" /> التخصيصات ({payment.allocations.length})
          </h3>
          {payment.allocations.length === 0 ? (
            <p className="text-sm text-gray-500 mt-2">لا توجد تخصيصات بعد.</p>
          ) : (
            <div className="overflow-x-auto mt-2">
              <table className="w-full text-sm">
                <thead>
                  <tr className="text-right text-xs text-gray-500 border-b">
                    <th className="py-2 pr-2">#</th>
                    <th className="py-2 pr-2">نوع المرجع</th>
                    <th className="py-2 pr-2">معرّف</th>
                    <th className="py-2 pr-2 text-left">المبلغ</th>
                  </tr>
                </thead>
                <tbody>
                  {payment.allocations.map((a, i) => (
                    <tr key={a.id} className="border-b">
                      <td className="py-2 pr-2 text-gray-500">{i + 1}</td>
                      <td className="py-2 pr-2">
                        <Badge variant="neutral">{PAYMENT_REF_TYPES[a.refType] || a.refType}</Badge>
                      </td>
                      <td className="py-2 pr-2 font-mono text-xs">{a.refId.slice(0, 8)}...</td>
                      <td className="py-2 pr-2 text-left font-mono font-semibold">{formatNumber(a.amountApplied)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>

        <div className="space-y-4">
          <Card>
            <h3 className="font-bold text-gray-800 mb-3 flex items-center gap-2">
              <FileText className="h-4 w-4" /> الملخص المالي
            </h3>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600">إجمالي الدفعة:</span>
                <span className="font-mono font-bold">{formatNumber(payment.amount)} {payment.currencyCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-green-700">المُخصَّص:</span>
                <span className="font-mono font-semibold text-green-700">{formatNumber(payment.allocatedAmount)}</span>
              </div>
              <div className="flex justify-between border-t pt-2 bg-purple-50 -mx-4 px-4 py-2 mt-2">
                <span className="font-bold text-purple-700">دفعة مقدمة (On Account):</span>
                <span className="font-mono font-bold text-purple-700 text-lg">{formatNumber(payment.onAccountAmount)}</span>
              </div>
            </div>
            {payment.onAccountAmount > 0 && canAllocate && (
              <p className="text-xs text-blue-700 mt-3 bg-blue-50 p-2 rounded">
                💡 يوجد مبلغ غير مخصص. اضغط &quot;تخصيص&quot; لتوزيعه على فواتير الموردين.
              </p>
            )}
          </Card>

          <Card>
            <h3 className="font-bold text-gray-800 mb-2">المعلومات</h3>
            <div className="space-y-1 text-xs text-gray-600">
              <p>• أُنشئت: <span className="font-mono">{formatDate(payment.createdAt)}</span></p>
              <p>• الحالة الحالية: <Badge variant={statusVariant} size="sm">{statusLabel}</Badge></p>
              <p>• نوع الحركة: {payment.partyType === 'Vendor' ? 'AP (دفع مورّد)' : 'AR (دفع/استرداد عميل)'}</p>
            </div>
          </Card>
        </div>
      </div>

      {/* Modal: تخصيص دفعة على فواتير */}
      <Modal
        open={allocModalOpen}
        onClose={() => setAllocModalOpen(false)}
        title="تخصيص الدفعة على فواتير"
        description={`المبلغ المتاح للتخصيص: ${formatNumber(payment.onAccountAmount)} ${payment.currencyCode}`}
        size="lg"
        footer={
          <>
            <Button variant="ghost" onClick={() => setAllocModalOpen(false)} disabled={actionLoading}>
              إلغاء
            </Button>
            <Button variant="primary" onClick={submitAllocations} loading={actionLoading} iconLeft={<Plus className="h-4 w-4" />}>
              حفظ التخصيصات
            </Button>
          </>
        }
      >
        {billsLoading ? (
          <p className="text-sm text-gray-500">جاري تحميل الفواتير...</p>
        ) : (
          <div className="space-y-3">
            {allocRows.map((row, i) => (
              <div key={i} className="grid grid-cols-12 gap-2 items-end">
                <div className="col-span-8">
                  <Select
                    label={i === 0 ? 'الفاتورة' : undefined}
                    value={row.refId}
                    onChange={(e) => updateAllocRow(i, { refId: e.target.value })}
                    options={[{ value: '', label: 'اختر فاتورة' }, ...billOptions]}
                  />
                </div>
                <div className="col-span-3">
                  <Input
                    label={i === 0 ? 'المبلغ' : undefined}
                    type="number"
                    value={row.amountApplied}
                    onChange={(e) => updateAllocRow(i, { amountApplied: e.target.value })}
                    min={0}
                    step="0.0001"
                  />
                </div>
                <div className="col-span-1 pb-1">
                  <button
                    type="button"
                    onClick={() => removeAllocRow(i)}
                    className="text-red-500 hover:text-red-700 p-2 disabled:opacity-30"
                    disabled={allocRows.length === 1}
                    title="حذف"
                  >
                    ✕
                  </button>
                </div>
              </div>
            ))}
            <Button type="button" variant="secondary" onClick={addAllocRow} iconLeft={<Plus className="h-4 w-4" />}>
              إضافة تخصيص
            </Button>
            <div className="border-t pt-3 text-sm flex justify-between">
              <span className="text-gray-600">إجمالي التخصيصات:</span>
              <span className={`font-mono font-bold ${totalAllocInput > payment.onAccountAmount ? 'text-red-600' : 'text-blue-700'}`}>
                {formatNumber(totalAllocInput)} {payment.currencyCode}
              </span>
            </div>
          </div>
        )}
      </Modal>
    </div>
  );
}
