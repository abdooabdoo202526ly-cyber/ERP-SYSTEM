'use client';

// صفحة إنشاء فاتورة مبيعات جديدة — pick customer + add lines + totals auto-calc + Save Draft/Post

import { useEffect, useState, useMemo } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Send, Plus, Trash2 } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { arApi, Customer, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

interface LineDraft {
  id: string;
  description: string;
  quantity: string;
  unitPrice: string;
  taxRate: string;
}

const emptyLine = (): LineDraft => ({
  id: crypto.randomUUID(),
  description: '',
  quantity: '1',
  unitPrice: '0',
  taxRate: '0',
});

export default function NewSalesInvoicePage() {
  const router = useRouter();
  const [customers, setCustomers] = useState<Customer[]>([]);
  const [customerId, setCustomerId] = useState<string>('');
  const [invoiceDate, setInvoiceDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState<string>('');
  const [currencyCode, setCurrencyCode] = useState('LYD');
  const [exchangeRate, setExchangeRate] = useState('1');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([emptyLine()]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    arApi.listCustomers()
      .then(setCustomers)
      .catch((e) => setError(getErrorMessage(e, 'تعذّر تحميل قائمة العملاء.')));
  }, []);

  const customerOptions = useMemo(
    () => [{ value: '', label: 'اختر العميل' }, ...customers.filter((c) => c.isActive).map((c) => ({ value: c.id, label: `${c.code} — ${c.name}` }))],
    [customers]
  );

  const updateLine = (id: string, patch: Partial<LineDraft>) => {
    setLines((ls) => ls.map((l) => (l.id === id ? { ...l, ...patch } : l)));
  };
  const removeLine = (id: string) => setLines((ls) => (ls.length > 1 ? ls.filter((l) => l.id !== id) : ls));
  const addLine = () => setLines((ls) => [...ls, emptyLine()]);

  const totals = useMemo(() => {
    let subtotal = 0, taxAmount = 0;
    for (const l of lines) {
      const qty = Number(l.quantity) || 0;
      const price = Number(l.unitPrice) || 0;
      const tax = Number(l.taxRate) || 0;
      const lineSub = qty * price;
      const lineTax = lineSub * tax;
      subtotal += lineSub;
      taxAmount += lineTax;
    }
    return { subtotal, taxAmount, total: subtotal + taxAmount };
  }, [lines]);

  const submit = async (postImmediately: boolean) => {
    setError(null);
    if (!customerId) {
      setError('الرجاء اختيار العميل.');
      return;
    }
    if (lines.every((l) => !l.description.trim() || Number(l.quantity) <= 0)) {
      setError('الرجاء إضافة بند واحد على الأقل بوصف وكمية صحيحة.');
      return;
    }
    setSubmitting(true);
    try {
      const payload = {
        customerId,
        invoiceDate: new Date(invoiceDate).toISOString(),
        dueDate: dueDate ? new Date(dueDate).toISOString() : undefined,
        currencyCode,
        exchangeRate: Number(exchangeRate) || 1,
        notes: notes || undefined,
        lines: lines
          .filter((l) => l.description.trim() && Number(l.quantity) > 0)
          .map((l) => ({
            description: l.description.trim(),
            quantity: Number(l.quantity),
            unitPrice: Number(l.unitPrice),
            taxRate: Number(l.taxRate) || 0,
          })),
        postImmediately,
      };
      const inv = await arApi.createInvoice(payload);
      router.push(`/finance/sales-invoices/${inv.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الفاتورة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ فاتورة مبيعات جديدة"
        description="إنشاء فاتورة جديدة بحالة مسودة أو مُرحلة مباشرة"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/sales-invoices' },
          { label: 'فواتير المبيعات', href: '/finance/sales-invoices' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/sales-invoices">
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
              label="العميل *"
              value={customerId}
              onChange={(e) => setCustomerId(e.target.value)}
              options={customerOptions}
            />
            <Input label="تاريخ الفاتورة *" type="date" value={invoiceDate} onChange={(e) => setInvoiceDate(e.target.value)} />
            <Input label="تاريخ الاستحقاق" type="date" value={dueDate} onChange={(e) => setDueDate(e.target.value)} />
            <div className="grid grid-cols-2 gap-2">
              <Input label="العملة" value={currencyCode} onChange={(e) => setCurrencyCode(e.target.value)} maxLength={3} />
              <Input label="سعر الصرف" type="number" value={exchangeRate} onChange={(e) => setExchangeRate(e.target.value)} step="0.00000001" />
            </div>
          </div>

          <h3 className="font-bold text-gray-800 pt-2">البنود</h3>
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500 border-b">
                  <th className="py-2 pr-2">#</th>
                  <th className="py-2 pr-2">الوصف</th>
                  <th className="py-2 pr-2">الكمية</th>
                  <th className="py-2 pr-2">السعر</th>
                  <th className="py-2 pr-2">الضريبة</th>
                  <th className="py-2 pr-2 text-left">المجموع</th>
                  <th className="py-2"></th>
                </tr>
              </thead>
              <tbody>
                {lines.map((l, i) => {
                  const qty = Number(l.quantity) || 0;
                  const price = Number(l.unitPrice) || 0;
                  const tax = Number(l.taxRate) || 0;
                  const lineTotal = qty * price;
                  return (
                    <tr key={l.id} className="border-b">
                      <td className="py-2 pr-2 text-gray-500">{i + 1}</td>
                      <td className="py-2 pr-2">
                        <Input value={l.description} onChange={(e) => updateLine(l.id, { description: e.target.value })} placeholder="وصف البند" />
                      </td>
                      <td className="py-2 pr-2">
                        <Input type="number" value={l.quantity} onChange={(e) => updateLine(l.id, { quantity: e.target.value })} min={0} step="0.0001" />
                      </td>
                      <td className="py-2 pr-2">
                        <Input type="number" value={l.unitPrice} onChange={(e) => updateLine(l.id, { unitPrice: e.target.value })} min={0} step="0.0001" />
                      </td>
                      <td className="py-2 pr-2">
                        <Input type="number" value={l.taxRate} onChange={(e) => updateLine(l.id, { taxRate: e.target.value })} min={0} max={1} step="0.0001" />
                      </td>
                      <td className="py-2 pr-2 text-left font-mono font-semibold">{formatNumber(lineTotal)}</td>
                      <td className="py-2 text-center">
                        <button type="button" onClick={() => removeLine(l.id)} className="text-red-500 hover:text-red-700 p-1" disabled={lines.length === 1}>
                          <Trash2 className="h-4 w-4" />
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <Button type="button" variant="secondary" onClick={addLine} iconLeft={<Plus className="h-4 w-4" />}>
            إضافة بند
          </Button>

          <h3 className="font-bold text-gray-800 pt-2">ملاحظات</h3>
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
            <div className="flex justify-between">
              <span className="text-gray-600">الإجمالي قبل الضريبة:</span>
              <span className="font-mono font-semibold">{formatNumber(totals.subtotal)}</span>
            </div>
            <div className="flex justify-between">
              <span className="text-gray-600">الضريبة:</span>
              <span className="font-mono font-semibold">{formatNumber(totals.taxAmount)}</span>
            </div>
            <div className="flex justify-between border-t pt-2">
              <span className="font-bold text-gray-800">الإجمالي:</span>
              <span className="font-mono font-bold text-blue-600 text-lg">{formatNumber(totals.total)} {currencyCode}</span>
            </div>
          </div>

          <div className="mt-6 space-y-2">
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
              حفظ وترحيل (Dr 1230 / Cr 5110)
            </Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
