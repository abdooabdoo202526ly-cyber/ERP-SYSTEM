'use client';

// صفحة إنشاء سند قبض — v1.0.28: Single-amount UX.
// اكتب المبلغ مرة واحدة فقط. الـ checkbox يقرر: تخصيص على فاتورة أو سند على الحساب.

import { useEffect, useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Send, FileText, Wallet, CheckCircle2 } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { arApi, Customer, SalesInvoice, PAYMENT_METHODS, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function NewReceiptPage() {
  const router = useRouter();
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [customerId, setCustomerId] = useState<string>('');
  const [openInvoices, setOpenInvoices] = useState<SalesInvoice[]>([]);
  const [receiptDate, setReceiptDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [amount, setAmount] = useState<string>('0');
  const [currencyCode, setCurrencyCode] = useState('LYD');
  const [paymentMethod, setPaymentMethod] = useState('');
  const [notes, setNotes] = useState('');
  const [allocateToInvoice, setAllocateToInvoice] = useState<boolean>(true);
  const [selectedInvoiceId, setSelectedInvoiceId] = useState<string>('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    arApi.listCustomers()
      .then(setCustomers)
      .catch((e) => setError(getErrorMessage(e, 'تعذّر تحميل العملاء.')));
  }, []);

  // عند تغيير العميل: جلب فواتيره المفتوحة
  useEffect(() => {
    if (!customerId) {
      setOpenInvoices([]);
      setSelectedInvoiceId('');
      return;
    }
    arApi.listInvoices()
      .then((all) => {
        const opens = all.filter((i) => i.customerId === customerId && i.outstanding > 0 && i.status !== 6);
        setOpenInvoices(opens);
        // Default-select the first invoice
        setSelectedInvoiceId(opens[0]?.id || '');
      })
      .catch(() => setOpenInvoices([]));
  }, [customerId]);

  const customerOptions = useMemo(
    () => [
      { value: '', label: 'اختر العميل' },
      ...customers.filter((c) => c.isActive).map((c) => ({
        value: c.id,
        label: `${c.code} — ${c.name.length > 30 ? c.name.slice(0, 30) + '…' : c.name}`,
      })),
    ],
    [customers]
  );

  const invoiceOptions = useMemo(() => {
    return [
      { value: '', label: '— بدون تخصيص —' },
      ...openInvoices.map((inv) => ({
        value: inv.id,
        label: `${inv.invoiceNumber} — متبقي: ${formatNumber(inv.outstanding)} ${inv.currencyCode}`,
      })),
    ];
  }, [openInvoices]);

  const paymentMethodOptions = useMemo(() => [
    { value: '', label: 'اختر طريقة الدفع' },
    ...Object.entries(PAYMENT_METHODS).map(([k, v]) => ({ value: k, label: v })),
  ], []);

  // v1.0.25: total outstanding for the selected customer
  const customerOutstanding = useMemo(
    () => openInvoices.reduce((s, inv) => s + (inv.outstanding || 0), 0),
    [openInvoices]
  );

  // الفاتورة المختارة (للعرض في الملخص)
  const selectedInvoice = useMemo(
    () => openInvoices.find((inv) => inv.id === selectedInvoiceId) || null,
    [openInvoices, selectedInvoiceId]
  );

  // v1.0.28: تحصيل كامل — يضبط المبلغ = المبلغ المتبقي للفاتورة المختارة
  const setAmountToInvoice = () => {
    if (selectedInvoice) setAmount(String(selectedInvoice.outstanding));
  };

  // v1.0.28: تحصيل كل المستحقات — يضبط المبلغ = مجموع مستحقات العميل
  const setAmountToOutstanding = () => {
    setAmount(String(customerOutstanding));
  };

  const submit = async (postImmediately: boolean) => {
    setError(null);
    if (!customerId) { setError('اختر العميل.'); return; }
    const amt = Number(amount);
    if (amt <= 0) { setError('مبلغ السند يجب أن يكون أكبر من صفر.'); return; }

    // v1.0.28: إذا اختار تخصيص، لازم يختار فاتورة
    if (allocateToInvoice && !selectedInvoiceId) {
      setError('اختر فاتورة للتخصيص، أو ألغِ خيار "تخصيص على فاتورة".');
      return;
    }
    if (allocateToInvoice && selectedInvoice && amt > selectedInvoice.outstanding + 0.0001) {
      setError(`المبلغ (${formatNumber(amt)}) أكبر من المتبقي على الفاتورة (${formatNumber(selectedInvoice.outstanding)}).`);
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        customerId,
        receiptDate: new Date(receiptDate).toISOString(),
        amount: amt,
        currencyCode,
        paymentMethod: paymentMethod || undefined,
        notes: notes || undefined,
        // v1.0.28: لو allocateToInvoice=false، ما نبعث allocations = سند على الحساب
        allocations: allocateToInvoice && selectedInvoiceId
          ? [{ salesInvoiceId: selectedInvoiceId, amountApplied: amt }]
          : [],
        postImmediately,
      };
      await arApi.createReceipt(payload);
      router.push(`/finance/receipts`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء سند القبض.'));
      setSubmitting(false);
    }
  };

  const canSubmit = !!customerId && Number(amount) > 0 && (!allocateToInvoice || !!selectedInvoiceId);

  return (
    <div>
      <PageHeader
        title="➕ سند قبض جديد"
        description="إنشاء سند قبض — اكتب المبلغ مرة واحدة، واختر التخصيص إن أردت"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/receipts' },
          { label: 'سندات القبض', href: '/finance/receipts' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/receipts">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        <Card className="lg:col-span-2 space-y-4">
          <h3 className="font-bold text-gray-800">معلومات السند</h3>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <Select
              label="العميل *"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              options={customerOptions}
            />
            <Input label="تاريخ السند *" type="date" value={receiptDate} onChange={(e) => setReceiptDate(e.target.value)} />
            <Input
              label="المبلغ *"
              type="number"
              value={amount}
              onChange={(e) => {
                const v = e.target.value;
                if (v.startsWith('-')) return;
                setAmount(v);
              }}
              min={0}
              step="0.0001"
            />
            <Input label="العملة" value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value)} maxLength={3} />
            <Select
              label="طريقة الدفع"
              value={paymentMethod}
              onChange={(e) => setPaymentMethod(e.target.value)}
              options={paymentMethodOptions}
            />
          </div>

          {/* v1.0.28: قسم التخصيص — قابل للطي/التفعيل */}
          <div className="border-t pt-4">
            <label className="flex items-center gap-2 cursor-pointer select-none">
              <input
                type="checkbox"
                checked={allocateToInvoice}
                onChange={(e) => setAllocateToInvoice(e.target.checked)}
                className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
              />
              <span className="font-semibold text-gray-800 flex items-center gap-1">
                {allocateToInvoice ? <FileText className="h-4 w-4 text-blue-600" /> : <Wallet className="h-4 w-4 text-gray-500" />}
                تخصيص على فاتورة
              </span>
              {!allocateToInvoice && (
                <span className="text-xs text-gray-500">(سند على الحساب — بدون تخصيص)</span>
              )}
            </label>

            {allocateToInvoice && (
              <div className="mt-3">
                {!customerId ? (
                  <p className="text-sm text-gray-500">اختر العميل أولاً لعرض فواتيره المفتوحة.</p>
                ) : openInvoices.length === 0 ? (
                  <p className="text-sm text-gray-500">لا توجد فواتير مفتوحة لهذا العميل.</p>
                ) : (
                  <>
                    <Select
                      label="الفاتورة"
                      value={selectedInvoiceId}
                      onChange={(e) => setSelectedInvoiceId(e.target.value)}
                      options={invoiceOptions}
                    />
                    {selectedInvoice && (
                      <div className="mt-2 p-3 rounded-lg bg-blue-50 border border-blue-200 text-sm">
                        <div className="flex justify-between">
                          <span className="text-gray-600">المتبقي على الفاتورة:</span>
                          <span className="font-mono font-bold text-blue-700">
                            {formatNumber(selectedInvoice.outstanding)} {selectedInvoice.currencyCode}
                          </span>
                        </div>
                        {Number(amount) > 0 && (
                          <div className="flex justify-between mt-1">
                            <span className="text-gray-600">سيتبقى بعد السند:</span>
                            <span className={`font-mono font-semibold ${
                              Number(amount) > selectedInvoice.outstanding + 0.0001 ? 'text-red-600' : 'text-green-600'
                            }`}>
                              {formatNumber(Math.max(0, selectedInvoice.outstanding - Number(amount)))} {selectedInvoice.currencyCode}
                            </span>
                          </div>
                        )}
                      </div>
                    )}
                  </>
                )}
              </div>
            )}
          </div>

          <h3 className="font-bold text-gray-800 pt-2 border-t">ملاحظات</h3>
          <textarea
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            rows={2}
            className="w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
            placeholder="ملاحظات اختيارية..."
          />
        </Card>

        <Card>
          <h3 className="font-bold text-gray-800 mb-3">الملخص</h3>
          <div className="space-y-2 text-sm">
            {customerId && customerOutstanding > 0 && (
              <>
                <div className="flex justify-between text-blue-700 bg-blue-50 -mx-3 px-3 py-2 rounded">
                  <span className="font-semibold">إجمالي المستحقات على العميل:</span>
                  <span className="font-mono font-bold">{formatNumber(customerOutstanding)} {currencyCode}</span>
                </div>
                <div className="flex justify-between text-xs text-gray-500">
                  <span>المتبقي بعد هذا السند:</span>
                  <span className="font-mono">{formatNumber(Math.max(0, customerOutstanding - Number(amount)))} {currencyCode}</span>
                </div>
              </>
            )}
            <div className="flex justify-between">
              <span className="text-gray-600">المبلغ:</span>
              <span className="font-mono font-semibold">{formatNumber(Number(amount))} {currencyCode}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">التخصيص:</span>
              <span className="font-mono font-semibold text-gray-700">
                {allocateToInvoice && selectedInvoice
                  ? `${formatNumber(Number(amount))} على ${selectedInvoice.invoiceNumber}`
                  : '— بدون —'}
              </span>
            </div>
            {allocateToInvoice && selectedInvoice && Number(amount) > 0 && (
              <div className="flex justify-between text-xs text-gray-500">
                <span>سيتبقى على الفاتورة:</span>
                <span className="font-mono">
                  {formatNumber(Math.max(0, selectedInvoice.outstanding - Number(amount)))} {selectedInvoice.currencyCode}
                </span>
              </div>
            )}
          </div>

          {/* أزرار مساعدة سريعة */}
          {customerId && (
            <div className="mt-4 space-y-2">
              {allocateToInvoice && selectedInvoice && (
                <button
                  type="button"
                  onClick={setAmountToInvoice}
                  className="w-full text-xs bg-purple-50 hover:bg-purple-100 text-purple-700 border border-purple-200 rounded px-2 py-2"
                  title="ضبط المبلغ = المتبقي على هذه الفاتورة"
                >
                  <CheckCircle2 className="h-3 w-3 inline-block ml-1" />
                  تحصيل كامل لهذه الفاتورة ({formatNumber(selectedInvoice.outstanding)})
                </button>
              )}
              {customerOutstanding > 0 && (
                <button
                  type="button"
                  onClick={setAmountToOutstanding}
                  className="w-full text-xs bg-amber-50 hover:bg-amber-100 text-amber-700 border border-amber-200 rounded px-2 py-2"
                  title="ضبط المبلغ = كل المستحقات على العميل"
                >
                  <CheckCircle2 className="h-3 w-3 inline-block ml-1" />
                  تحصيل كل المستحقات ({formatNumber(customerOutstanding)})
                </button>
              )}
            </div>
          )}

          <div className="mt-6 space-y-2">
            <Button
              type="button"
              variant="secondary"
              loading={submitting}
              onClick={() => submit(false)}
              iconLeft={<Save className="h-4 w-4" />}
              className="w-full"
              disabled={!canSubmit}
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
              disabled={!canSubmit}
            >
              حفظ وترحيل (Dr 1210 / Cr 1230)
            </Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
