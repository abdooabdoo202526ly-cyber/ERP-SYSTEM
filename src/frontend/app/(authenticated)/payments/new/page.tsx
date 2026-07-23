'use client';

// صفحة إنشاء دفعة جديدة — pick party (vendor) + amount + allocations اختيارية
// تستهلك: GET /api/procurement/vendors + GET /api/procurement/bills + POST /api/payments

import { useEffect, useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Send, Plus, Trash2, ShoppingBag, FileText } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader, Badge } from '@/components/ui';
import {
  paymentsApi,
  procurementApi,
  Vendor,
  VendorBill,
  PAYMENT_METHODS,
  PAYMENT_PARTY_TYPES,
  getErrorMessage,
} from '@/lib/api';
import { formatNumber } from '@/lib/format';

interface AllocDraft {
  id: string;
  refId: string;
  amountApplied: string;
}

const emptyAlloc = (): AllocDraft => ({
  id: crypto.randomUUID(),
  refId: '',
  amountApplied: '0',
});

export default function NewPaymentPage() {
  const router = useRouter();

  // Master data
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [bills, setBills] = useState<VendorBill[]>([]);
  const [loadingMaster, setLoadingMaster] = useState(true);

  // Form
  const [vendorId, setVendorId] = useState<string>('');
  const [paymentDate, setPaymentDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<string>('0');
  const [currencyCode, setCurrencyCode] = useState<string>('LYD');
  const [paymentMethod, setPaymentMethod] = useState<string>('Cash');
  const [notes, setNotes] = useState<string>('');
  const [allocations, setAllocations] = useState<AllocDraft[]>([emptyAlloc()]);

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoadingMaster(true);
    Promise.all([procurementApi.listVendors(), procurementApi.listBills()])
      .then(([v, b]) => {
        setVendors(v.filter((x) => x.isActive));
        setBills(b.filter((x) => x.status === 2 || x.status === 3)); // Posted or Paid
      })
      .catch((e) => setError(getErrorMessage(e, 'تعذّر تحميل البيانات الأساسية.')))
      .finally(() => setLoadingMaster(false));
  }, []);

  // عند اختيار مورّد: تعيين العملة من المورّد + فلترة الفواتير
  useEffect(() => {
    if (!vendorId) return;
    const v = vendors.find((x) => x.id === vendorId);
    if (v) {
      setCurrencyCode(v.currency || 'LYD');
    }
    // تنظيف التخصيصات التي لم تعد تخصّ المورّد
    setAllocations((allocs) => allocs.filter((a) => !a.refId));
  }, [vendorId, vendors]);

  // فواتير المورّد المختار (المُرحَّلة فقط)
  const vendorBills = useMemo(
    () => bills.filter((b) => b.vendorId === vendorId),
    [bills, vendorId]
  );

  const vendorOptions = useMemo(
    () => [
      { value: '', label: 'اختر المورّد' },
      ...vendors.map((v) => ({ value: v.id, label: `${v.name}` })),
    ],
    [vendors]
  );

  const methodOptions = useMemo(
    () => Object.entries(PAYMENT_METHODS).map(([k, v]) => ({ value: k, label: v })),
    []
  );

  const updateAlloc = (id: string, patch: Partial<AllocDraft>) => {
    setAllocations((a) => a.map((row) => (row.id === id ? { ...row, ...patch } : row)));
  };
  const removeAlloc = (id: string) => {
    setAllocations((a) => (a.length > 1 ? a.filter((r) => r.id !== id) : a));
  };
  const addAlloc = () => setAllocations((a) => [...a, emptyAlloc()]);

  const allocOptions = useMemo(() => {
    return vendorBills.map((b) => ({
      value: b.id,
      label: `${b.billNumber} — ${formatNumber(b.totalAmount)} ${b.currency}`,
    }));
  }, [vendorBills]);

  // مجموع التخصيصات
  const totalAlloc = useMemo(
    () => allocations.reduce((s, r) => s + (Number(r.amountApplied) || 0), 0),
    [allocations]
  );

  const onAccount = useMemo(() => {
    const amt = Number(amount) || 0;
    return Math.max(0, amt - totalAlloc);
  }, [amount, totalAlloc]);

  const validate = (): string | null => {
    if (!vendorId) return 'الرجاء اختيار المورّد.';
    const amt = Number(amount);
    if (!amt || amt <= 0) return 'الرجاء إدخال مبلغ أكبر من صفر.';
    const validAllocs = allocations.filter((a) => a.refId && Number(a.amountApplied) > 0);
    if (totalAlloc > amt) {
      return `مجموع التخصيصات (${formatNumber(totalAlloc)}) أكبر من مبلغ الدفعة (${formatNumber(amt)}).`;
    }
    for (const a of validAllocs) {
      const bill = vendorBills.find((b) => b.id === a.refId);
      if (!bill) return 'فاتورة مختارة غير متاحة.';
      if (Number(a.amountApplied) > bill.totalAmount) {
        return `مبلغ التخصيص (${formatNumber(Number(a.amountApplied))}) أكبر من إجمالي الفاتورة (${formatNumber(bill.totalAmount)}).`;
      }
    }
    return null;
  };

  const submit = async (postImmediately: boolean) => {
    setError(null);
    const v = validate();
    if (v) {
      setError(v);
      return;
    }
    setSubmitting(true);
    try {
      const validAllocs = allocations
        .filter((a) => a.refId && Number(a.amountApplied) > 0)
        .map((a) => ({
          refType: 'VendorBill' as const,
          refId: a.refId,
          amountApplied: Number(a.amountApplied),
        }));

      const payload = {
        partyType: 'Vendor' as const,
        partyId: vendorId,
        paymentDate: new Date(paymentDate).toISOString(),
        amount: Number(amount),
        currencyCode,
        paymentMethod,
        notes: notes || undefined,
        allocations: validAllocs,
      };
      const payment = await paymentsApi.create(payload);

      if (postImmediately) {
        try {
          const posted = await paymentsApi.post(payment.id);
          router.push(`/payments/${posted.id}`);
          return;
        } catch (e: unknown) {
          // إن فشل الترحيل، انتقل للتفاصيل ليُشاهد المستخدم الحالة Draft
          router.push(`/payments/${payment.id}`);
          return;
        }
      }
      router.push(`/payments/${payment.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الدفعة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ دفعة جديدة"
        description="إنشاء سند دفع لمورّد (AP) — Dr 2210 / Cr 1210 عند الترحيل"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المدفوعات', href: '/payments' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/payments">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2 space-y-4">
          <h3 className="font-bold text-gray-800">معلومات أساسية</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <Select
              label="المورّد *"
              value={vendorId}
              onChange={(e) => setVendorId(e.target.value)}
              options={vendorOptions}
              disabled={loadingMaster}
            />
            <Input label="تاريخ الدفعة *" type="date" value={paymentDate} onChange={(e) => setPaymentDate(e.target.value)} />
            <Input
              label="المبلغ *"
              type="number"
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              min={0}
              step="0.0001"
            />
            <div className="grid grid-cols-2 gap-2">
              <Input
                label="العملة"
                value={currencyCode}
                onChange={(e) => setCurrencyCode(e.target.value.toUpperCase())}
                maxLength={3}
              />
              <Select
                label="طريقة الدفع"
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value)}
                options={methodOptions}
              />
            </div>
          </div>

          <h3 className="font-bold text-gray-800 pt-2 flex items-center gap-2">
            <ShoppingBag className="h-4 w-4" /> تخصيصات على فواتير المورّد (اختيارية)
          </h3>
          {vendorId && vendorBills.length === 0 && (
            <p className="text-sm text-gray-500 bg-gray-50 p-3 rounded">
              لا توجد فواتير مُرحَّلة لهذا المورّد. يمكنك إنشاء الدفعة كـ On Account (دفعة مقدمة) وتخصيصها لاحقاً.
            </p>
          )}
          {vendorId && vendorBills.length > 0 && (
            <div className="space-y-2">
              {allocations.map((row, i) => {
                const bill = vendorBills.find((b) => b.id === row.refId);
                return (
                  <div key={row.id} className="grid grid-cols-12 gap-2 items-end p-2 bg-gray-50 rounded">
                    <div className="col-span-7">
                      <Select
                        label={i === 0 ? 'الفاتورة' : undefined}
                        value={row.refId}
                        onChange={(e) => updateAlloc(row.id, { refId: e.target.value })}
                        options={[{ value: '', label: 'اختر فاتورة' }, ...allocOptions]}
                      />
                    </div>
                    <div className="col-span-4">
                      <Input
                        label={i === 0 ? 'المبلغ المخصَّص' : undefined}
                        type="number"
                        value={row.amountApplied}
                        onChange={(e) => updateAlloc(row.id, { amountApplied: e.target.value })}
                        min={0}
                        step="0.0001"
                      />
                    </div>
                    <div className="col-span-1 pb-1">
                      <button
                        type="button"
                        onClick={() => removeAlloc(row.id)}
                        className="text-red-500 hover:text-red-700 p-2 disabled:opacity-30"
                        disabled={allocations.length === 1}
                        title="حذف"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </div>
                    {bill && (
                      <div className="col-span-12 -mt-1">
                        <p className="text-xs text-gray-500">
                          إجمالي الفاتورة: <span className="font-mono font-semibold">{formatNumber(bill.totalAmount)} {bill.currency}</span>
                        </p>
                      </div>
                    )}
                  </div>
                );
              })}
              <Button type="button" variant="secondary" onClick={addAlloc} iconLeft={<Plus className="h-4 w-4" />}>
                إضافة تخصيص
              </Button>
            </div>
          )}

          <h3 className="font-bold text-gray-800 pt-2">ملاحظات</h3>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={3}
            className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
            placeholder="ملاحظات اختيارية (مرجع الشيك، رقم التحويل...)"
          />
        </Card>

        <div className="space-y-4">
          <Card>
            <h3 className="font-bold text-gray-800 mb-3">الملخص</h3>
            <div className="space-y-2 text-sm">
              <div className="flex justify-between">
                <span className="text-gray-600">المبلغ:</span>
                <span className="font-mono font-semibold">{formatNumber(Number(amount) || 0)} {currencyCode}</span>
              </div>
              <div className="flex justify-between">
                <span className="text-gray-600">إجمالي التخصيصات:</span>
                <span className="font-mono font-semibold text-green-700">{formatNumber(totalAlloc)}</span>
              </div>
              <div className="flex justify-between border-t pt-2">
                <span className="font-bold text-purple-700">دفعة مقدمة (On Account):</span>
                <span className="font-mono font-bold text-purple-700">{formatNumber(onAccount)}</span>
              </div>
            </div>
          </Card>

          {vendorId && (
            <Card>
              <h3 className="font-bold text-gray-800 mb-2 flex items-center gap-2">
                <FileText className="h-4 w-4" /> فواتير المورّد
              </h3>
              {vendorBills.length === 0 ? (
                <p className="text-xs text-gray-500">لا توجد فواتير مُرحَّلة.</p>
              ) : (
                <ul className="space-y-1 text-xs">
                  {vendorBills.slice(0, 5).map((b) => (
                    <li key={b.id} className="flex justify-between border-b pb-1">
                      <span className="font-mono">{b.billNumber}</span>
                      <span className="font-mono font-semibold">{formatNumber(b.totalAmount)} {b.currency}</span>
                    </li>
                  ))}
                  {vendorBills.length > 5 && (
                    <li className="text-gray-400 text-center">+ {vendorBills.length - 5} أخرى</li>
                  )}
                </ul>
              )}
            </Card>
          )}

          <Card>
            <h3 className="font-bold text-gray-800 mb-2">عند الترحيل</h3>
            <p className="text-xs text-gray-600 mb-2">سيُنشأ القيد التالي:</p>
            <table className="w-full text-xs">
              <tbody>
                <tr className="border-b">
                  <td className="py-1 font-mono">2210 AP</td>
                  <td className="py-1 text-green-700 text-left font-mono">Dr</td>
                  <td className="py-1 text-left font-mono font-semibold">{formatNumber(Number(amount) || 0)}</td>
                </tr>
                <tr>
                  <td className="py-1 font-mono">1210 Cash</td>
                  <td className="py-1 text-red-700 text-left font-mono">Cr</td>
                  <td className="py-1 text-left font-mono font-semibold">{formatNumber(Number(amount) || 0)}</td>
                </tr>
              </tbody>
            </table>
            <Badge variant="info" className="mt-2">نوع الطرف: {PAYMENT_PARTY_TYPES.Vendor}</Badge>
          </Card>

          <div className="space-y-2">
            <Button
              type="button"
              variant="secondary"
              loading={submitting}
              onClick={() => submit(false)}
              iconLeft={<Save className="h-4 w-4" />}
              className="w-full"
            >
              حفظ كمسودة
            </Button>
            <Button
              type="button"
              variant="primary"
              loading={submitting}
              onClick={() => submit(true)}
              iconLeft={<Send className="h-4 w-4" />}
              className="w-full"
            >
              حفظ وترحيل
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
