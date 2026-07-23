'use client';

// صفحة تعديل فاتورة المورّد (Vendor Bill Edit) — مُعبّأ مسبقاً، متاح فقط لو الحالة Draft.

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Lock } from 'lucide-react';
import { Button, Input, Card, PageHeader, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  VendorBill,
  BILL_STATUSES,
  BILL_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

interface LineDraft {
  itemId: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export default function EditBillPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [bill, setBill] = useState<VendorBill | null>(null);
  const [billDate, setBillDate] = useState(new Date().toISOString().slice(0, 10));
  const [dueDate, setDueDate] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState(false);

  useEffect(() => {
    const load = async () => {
      if (!params?.id) return;
      setLoading(true);
      setError(null);
      try {
        const b = await procurementApi.getBill(params.id);
        setBill(b);
        setBillDate(b.billDate ? b.billDate.slice(0, 10) : new Date().toISOString().slice(0, 10));
        setDueDate(b.dueDate ? b.dueDate.slice(0, 10) : '');
        setCurrency(b.currency || 'LYD');
        setNotes(b.notes || '');
        setLines(
          (b.lines || []).map((l) => ({
            itemId: l.itemId,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            taxRate: l.taxRate,
          }))
        );
        // الحظر لو الحالة ليست Draft (1)
        if (b.status !== 1) {
          setBlocked(true);
        }
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'تعذّر تحميل الفاتورة.'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params?.id]);

  const subTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const taxTotal = lines.reduce(
    (sum, l) => sum + l.quantity * l.unitPrice * (l.taxRate / 100),
    0
  );
  const grandTotal = subTotal + taxTotal;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!bill) return;
    setError(null);

    if (blocked) {
      setError('لا يمكن تعديل فاتورة في هذه الحالة.');
      return;
    }
    if (lines.some((l) => l.unitPrice <= 0)) {
      setError('يجب إدخال سعر صحيح لكل بند.');
      return;
    }

    setSubmitting(true);
    try {
      // الـ backend لا يدعم PUT لتعديل الفواتير — نحذف ثم ننشئ
      // بديل عملي: نحذف الفاتورة الحالية (إن أمكن) ثم ننشئ جديدة بنفس البيانات.
      // لكن الحذف قد لا يكون متاحاً — لذا نُعلم المستخدم.
      const confirmReplace = window.confirm(
        'الـ backend لا يدعم تعديل Vendor Bill مباشرةً. هل تريد إنشاء فاتورة جديدة بنفس البيانات؟ (ستبقى الفاتورة الحالية كما هي)'
      );
      if (!confirmReplace) {
        setSubmitting(false);
        return;
      }
      const newBill = await procurementApi.createBill({
        goodsReceiptId: bill.goodsReceiptId,
        vendorId: bill.vendorId,
        billDate,
        dueDate: dueDate || undefined,
        currency,
        notes: notes || undefined,
        lines: lines.map((l, i) => ({
          id: `temp-${i}`,
          itemId: l.itemId,
          quantity: l.quantity,
          unitPrice: l.unitPrice,
          taxRate: l.taxRate,
          subTotal: l.quantity * l.unitPrice,
        })),
      } as Partial<VendorBill>);
      router.push(`/procurement/bills/${newBill.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الفاتورة.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="تعديل فاتورة مورّد" />
        <Card className="max-w-4xl">
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (blocked && bill) {
    return (
      <div>
        <PageHeader
          title={`تعديل فاتورة: ${bill.billNumber}`}
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'فواتير الموردين', href: '/procurement/bills' },
            { label: bill.billNumber, href: `/procurement/bills/${bill.id}` },
            { label: 'تعديل' },
          ]}
          actions={
            <Link href={`/procurement/bills/${bill.id}`}>
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع للتفاصيل
              </Button>
            </Link>
          }
        />
        <Card className="max-w-2xl">
          <div className="bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-3 rounded-lg text-sm flex items-start gap-2">
            <Lock className="h-5 w-5 flex-shrink-0 mt-0.5" />
            <div>
              <p className="font-semibold mb-1">لا يمكن تعديل الفاتورة في هذه الحالة</p>
              <p>
                الفاتورة في حالة{' '}
                <Badge variant={BILL_STATUS_VARIANTS[bill.status] || 'neutral'}>
                  {BILL_STATUSES[bill.status] || bill.status}
                </Badge>{' '}
                — التعديل مسموح فقط في حالة <strong>مسودة (Draft)</strong>.
              </p>
            </div>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={`✏️ تعديل فاتورة: ${bill?.billNumber || ''}`}
        description="تعديل الفاتورة (مسموح فقط في حالة Draft)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'فواتير الموردين', href: '/procurement/bills' },
          { label: bill?.billNumber || '', href: `/procurement/bills/${params.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/procurement/bills/${params.id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للتفاصيل
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4 max-w-4xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error}
          </div>
        )}

        <Card title="معلومات الفاتورة">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
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
            <div>
              <p className="text-xs text-gray-500 mb-1">سند الاستلام</p>
              <p className="text-sm font-mono text-gray-700">
                {bill?.goodsReceiptId?.slice(0, 8) || '—'}…
              </p>
            </div>
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
          <Card title="بنود الفاتورة">
            <div className="space-y-3">
              {lines.map((line, idx) => (
                <div
                  key={idx}
                  className="grid grid-cols-12 gap-2 items-end p-3 bg-gray-50 rounded-lg"
                >
                  <div className="col-span-12 md:col-span-4">
                    <p className="text-xs text-gray-500">الصنف</p>
                    <p className="text-sm font-mono text-gray-800">
                      {line.itemId?.slice(0, 8) || '—'}…
                    </p>
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
                      onChange={(e) =>
                        setLines((prev) =>
                          prev.map((l, i) =>
                            i === idx ? { ...l, unitPrice: Number(e.target.value) } : l
                          )
                        )
                      }
                    />
                  </div>
                  <div className="col-span-12 md:col-span-2">
                    <Input
                      label={idx === 0 ? 'الضريبة %' : undefined}
                      type="number"
                      min={0}
                      step={0.1}
                      value={line.taxRate}
                      onChange={(e) =>
                        setLines((prev) =>
                          prev.map((l, i) =>
                            i === idx ? { ...l, taxRate: Number(e.target.value) } : l
                          )
                        )
                      }
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

        <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-xs text-amber-800">
          💡 ملاحظة: الـ backend الحالي يوفّر إنشاء فواتير الموردين فقط (لا يوجد PUT لتعديل Bill).
          عند الحفظ، سيتم إنشاء فاتورة جديدة بنفس البيانات. الفاتورة الأصلية ستبقى كما هي.
        </div>

        <div className="flex items-center gap-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
            disabled={lines.length === 0}
          >
            حفظ التغييرات
          </Button>
          <Link href={`/procurement/bills/${params.id}`}>
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
