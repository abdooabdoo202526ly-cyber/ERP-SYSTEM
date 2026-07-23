'use client';

// صفحة إنشاء فاتورة مورّد جديدة (Vendor Bill) — يختار GR ويعرض الـ lines

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Select, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  GoodsReceipt,
  VendorBillLine,
  getErrorMessage,
} from '@/lib/api';

interface LineDraft {
  itemId: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export default function NewBillPage() {
  const router = useRouter();
  useAuth();
  const [grs, setGRs] = useState<GoodsReceipt[]>([]);
  const [grId, setGrId] = useState('');
  const [billDate, setBillDate] = useState(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    load();
  }, []);

  const load = async () => {
    setLoading(true);
    try {
      const data = await procurementApi.listGRs();
      // v1.0.31: نعرض كل الـ GRs ما عدا المُلغاة (مرونة)
      // الـ backend يتحقق من الـ status الفعلي عند الإنشاء
      const open = data.filter((g) => g.status !== 3); // 3 = Cancelled
      setGRs(open);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل البيانات.'));
    } finally {
      setLoading(false);
    }
  };

  const grOptions = grs.map((g) => ({
    label: `${g.grNumber} — ${g.vendorName || g.poNumber || ''}`,
    value: g.id,
  }));

  // عند اختيار GR، نملأ الـ lines (الأسعار = 0 افتراضياً، المستخدم يدخلها)
  useEffect(() => {
    if (!grId) {
      setLines([]);
      return;
    }
    const gr = grs.find((g) => g.id === grId);
    if (gr && gr.lines) {
      setLines(
        gr.lines.map((l) => ({
          itemId: l.itemId,
          quantity: l.quantity,
          unitPrice: 0,
          taxRate: 0,
        }))
      );
      if (gr.currency) setCurrency(gr.currency);
    }
  }, [grId, grs]);

  const updateLine = (idx: number, key: keyof LineDraft, value: string | number) => {
    setLines((prev) =>
      prev.map((l, i) =>
        i === idx
          ? {
              ...l,
              [key]:
                typeof value === 'string' && (key === 'quantity' || key === 'unitPrice' || key === 'taxRate')
                  ? Number(value)
                  : value,
            }
          : l
      )
    );
  };

  const subTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const taxTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice * (l.taxRate / 100), 0);
  const grandTotal = subTotal + taxTotal;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!grId) {
      setError('يجب اختيار سند الاستلام.');
      return;
    }
    if (lines.some((l) => l.unitPrice <= 0)) {
      setError('يجب إدخال سعر صحيح لكل بند.');
      return;
    }

    setSubmitting(true);
    try {
      const linesDto: VendorBillLine[] = lines.map((l, i) => ({
        id: `temp-${i}`,
        itemId: l.itemId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxRate: l.taxRate,
        subTotal: l.quantity * l.unitPrice,
      }));
      const gr = grs.find((g) => g.id === grId);
      await procurementApi.createBill({
        goodsReceiptId: grId,
        vendorId: (gr as unknown as { vendorId?: string })?.vendorId || '',
        billDate,
        dueDate: dueDate || undefined,
        currency,
        notes: notes || undefined,
        lines: linesDto,
      } as Partial<{ goodsReceiptId: string; vendorId: string; billDate: string; dueDate?: string; currency: string; notes?: string; lines: VendorBillLine[] }>);
      router.push('/procurement/bills');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الفاتورة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ فاتورة مورّد جديدة"
        description="إنشاء Vendor Bill من سند استلام"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'فواتير الموردين', href: '/procurement/bills' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/procurement/bills">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4 max-w-4xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error}</div>
        )}

        <Card title="معلومات الفاتورة">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="سند الاستلام *"
              value={grId}
              onChange={(e) => setGrId(e.target.value)}
              options={grOptions}
              placeholder={loading ? 'جاري التحميل...' : 'اختر GR'}
              required
            />
            <Input
              label="العملة"
              value={currency}
              onChange={(e) => setCurrency(e.target.value)}
              maxLength={3}
            />
            <Input
              type="date"
              label="تاريخ الفاتورة"
              value={billDate}
              onChange={(e) => setBillDate(e.target.value)}
              required
            />
            <Input
              type="date"
              label="تاريخ الاستحقاق"
              value={dueDate}
              onChange={(e) => setDueDate(e.target.value)}
            />
          </div>
          <div className="mt-4">
            <label className="block text-sm font-medium text-gray-700 mb-1">ملاحظات</label>
            <textarea
              value={notes}
              onChange={(e) => setNotes(e.target.value)}
              rows={2}
              className="w-full rounded-lg border border-gray-300 px-3 py-2 text-sm focus:border-blue-500 focus:ring-2 focus:ring-blue-200"
            />
          </div>
        </Card>

        {lines.length > 0 && (
          <Card title="بنود الفاتورة (مأخوذة من GR)">
            <div className="space-y-3">
              {lines.map((line, idx) => (
                <div
                  key={idx}
                  className="grid grid-cols-12 gap-2 items-end p-3 bg-gray-50 rounded-lg"
                >
                  <div className="col-span-12 md:col-span-4">
                    <p className="text-xs text-gray-500">الصنف</p>
                    <p className="text-sm font-mono text-gray-800">{line.itemId}</p>
                  </div>
                  <div className="col-span-6 md:col-span-3">
                    <Input
                      label={idx === 0 ? 'الكمية' : undefined}
                      type="number"
                      value={line.quantity}
                      readOnly
                      disabled
                    />
                  </div>
                  <div className="col-span-6 md:col-span-3">
                    <Input
                      label={idx === 0 ? 'سعر الوحدة' : undefined}
                      type="number"
                      min={0}
                      step={0.01}
                      value={line.unitPrice}
                      onChange={(e) => updateLine(idx, 'unitPrice', e.target.value)}
                    />
                  </div>
                  <div className="col-span-12 md:col-span-2">
                    <Input
                      label={idx === 0 ? 'الضريبة %' : undefined}
                      type="number"
                      min={0}
                      step={0.1}
                      value={line.taxRate}
                      onChange={(e) => updateLine(idx, 'taxRate', e.target.value)}
                    />
                  </div>
                </div>
              ))}
            </div>

            <div className="mt-4 pt-4 border-t flex justify-end">
              <div className="text-end text-sm space-y-1 w-64">
                <div className="flex justify-between">
                  <span className="text-gray-500">المجموع الفرعي:</span>
                  <span className="font-mono font-semibold">
                    {subTotal.toFixed(2)} {currency}
                  </span>
                </div>
                <div className="flex justify-between">
                  <span className="text-gray-500">الضريبة:</span>
                  <span className="font-mono font-semibold">
                    {taxTotal.toFixed(2)} {currency}
                  </span>
                </div>
                <div className="flex justify-between text-base font-bold pt-1 border-t">
                  <span>الإجمالي:</span>
                  <span className="text-blue-700">
                    {grandTotal.toFixed(2)} {currency}
                  </span>
                </div>
              </div>
            </div>
          </Card>
        )}

        <div className="flex items-center gap-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
            disabled={lines.length === 0}
          >
            حفظ الفاتورة
          </Button>
          <Link href="/procurement/bills">
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
