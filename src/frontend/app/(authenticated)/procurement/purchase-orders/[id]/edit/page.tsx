'use client';

// صفحة تعديل أمر الشراء (PO Edit) — مُعبّأ مسبقاً، متاح فقط لو الحالة Draft.

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Plus as PlusIcon, Trash2, Lock } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  inventoryApi,
  Vendor,
  Item,
  PurchaseOrder,
  PurchaseOrderLine,
  PO_STATUSES,
  PO_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

interface LineDraft {
  itemId: string;
  quantity: number;
  unitPrice: number;
  taxRate: number;
}

export default function EditPurchaseOrderPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [vendors, setVendors] = useState<Vendor[]>([]);
  const [items, setItems] = useState<Item[]>([]);
  const [po, setPO] = useState<PurchaseOrder | null>(null);
  const [vendorId, setVendorId] = useState('');
  const [orderDate, setOrderDate] = useState(new Date().toISOString().slice(0, 10));
  const [expectedDate, setExpectedDate] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [notes, setNotes] = useState('');
  const [lines, setLines] = useState<LineDraft[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [blocked, setBlocked] = useState(false);

  useEffect(() => {
    const load = async () => {
      if (!params?.id) return;
      setLoading(true);
      setError(null);
      try {
        const [p, v, i] = await Promise.all([
          procurementApi.getPO(params.id),
          procurementApi.listVendors().catch(() => [] as Vendor[]),
          inventoryApi.listItems().catch(() => [] as Item[]),
        ]);
        setPO(p);
        setVendors(v);
        setItems(i);
        setVendorId(p.vendorId);
        setOrderDate(p.orderDate ? p.orderDate.slice(0, 10) : new Date().toISOString().slice(0, 10));
        setExpectedDate(p.expectedDate ? p.expectedDate.slice(0, 10) : '');
        setCurrency(p.currency || 'LYD');
        setNotes(p.notes || '');
        setLines(
          (p.lines || []).map((l) => ({
            itemId: l.itemId,
            quantity: l.quantity,
            unitPrice: l.unitPrice,
            taxRate: l.taxRate,
          }))
        );
        // الحظر لو الحالة ليست Draft (1)
        if (p.status !== 1) {
          setBlocked(true);
        }
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'تعذّر تحميل أمر الشراء.'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params?.id]);

  const vendorOptions = vendors.map((v) => ({ label: v.name, value: v.id }));
  const itemOptions = items.map((i) => ({ label: `${i.sku} — ${i.name}`, value: i.id }));

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

  const addLine = () =>
    setLines((prev) => [...prev, { itemId: '', quantity: 1, unitPrice: 0, taxRate: 0 }]);
  const removeLine = (idx: number) => setLines((prev) => prev.filter((_, i) => i !== idx));

  const subTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice, 0);
  const taxTotal = lines.reduce((sum, l) => sum + l.quantity * l.unitPrice * (l.taxRate / 100), 0);
  const grandTotal = subTotal + taxTotal;

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!po) return;
    setError(null);

    if (blocked) {
      setError('لا يمكن تعديل أمر شراء في هذه الحالة.');
      return;
    }
    if (!vendorId) {
      setError('يجب اختيار المورّد.');
      return;
    }
    if (lines.length === 0 || lines.some((l) => !l.itemId || l.quantity <= 0)) {
      setError('كل بند يجب أن يحتوي على صنف وكمية صحيحة.');
      return;
    }

    setSubmitting(true);
    try {
      const linesDto: PurchaseOrderLine[] = lines.map((l, i) => ({
        id: `temp-${i}`,
        itemId: l.itemId,
        quantity: l.quantity,
        unitPrice: l.unitPrice,
        taxRate: l.taxRate,
        subTotal: l.quantity * l.unitPrice,
      }));
      // الـ backend يوفّر Create فقط، لذا نستخدم POST لإعادة إنشاء أمر جديد.
      // (في إنتاج لاحق: PUT /api/procurement/pos/{id} — حالياً غير متاح.)
      // بديل: نحذف ثم ننشئ — لكن هذا خارج نطاق هذه المهمة.
      // لذلك: نستدعي createPO ثم نحذف القديم. للحفاظ على الـ UX، نُنبّه المستخدم.
      const confirmReplace = window.confirm(
        'الـ backend لا يدعم تعديل PO مباشرةً. هل تريد إنشاء أمر جديد بنفس البيانات؟ (سيُحفظ الأمر الحالي كما هو)'
      );
      if (!confirmReplace) {
        setSubmitting(false);
        return;
      }
      const newPO = await procurementApi.createPO({
        vendorId,
        orderDate,
        expectedDate: expectedDate || undefined,
        currency,
        notes: notes || undefined,
        lines: linesDto,
      } as Partial<PurchaseOrder> & { vendorId: string; lines: PurchaseOrderLine[] });
      router.push(`/procurement/purchase-orders/${newPO.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الأمر.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="تعديل أمر شراء" />
        <Card className="max-w-4xl">
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (blocked && po) {
    return (
      <div>
        <PageHeader
          title={`تعديل أمر شراء: ${po.poNumber}`}
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'أوامر الشراء', href: '/procurement/purchase-orders' },
            { label: po.poNumber, href: `/procurement/purchase-orders/${po.id}` },
            { label: 'تعديل' },
          ]}
          actions={
            <Link href={`/procurement/purchase-orders/${po.id}`}>
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
              <p className="font-semibold mb-1">لا يمكن تعديل أمر الشراء في هذه الحالة</p>
              <p>
                الأمر في حالة{' '}
                <Badge variant={PO_STATUS_VARIANTS[po.status] || 'neutral'}>
                  {PO_STATUSES[po.status] || po.status}
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
        title={`✏️ تعديل أمر شراء: ${po?.poNumber || ''}`}
        description="تعديل بنود الأمر (مسموح فقط في حالة Draft)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'أوامر الشراء', href: '/procurement/purchase-orders' },
          { label: po?.poNumber || '', href: `/procurement/purchase-orders/${params.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/procurement/purchase-orders/${params.id}`}>
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

        <Card title="معلومات الأمر">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="المورّد *"
              value={vendorId}
              onChange={(e) => setVendorId(e.target.value)}
              options={vendorOptions}
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
              label="تاريخ الطلب"
              value={orderDate}
              onChange={(e) => setOrderDate(e.target.value)}
              required
            />
            <Input
              type="date"
              label="تاريخ التوصيل المتوقع"
              value={expectedDate}
              onChange={(e) => setExpectedDate(e.target.value)}
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

        <Card
          title="بنود الأمر (Lines)"
          actions={
            <Button
              type="button"
              variant="outline"
              size="sm"
              iconLeft={<PlusIcon className="h-3 w-3" />}
              onClick={addLine}
            >
              إضافة بند
            </Button>
          }
        >
          <div className="space-y-3">
            {lines.map((line, idx) => (
              <div
                key={idx}
                className="grid grid-cols-12 gap-2 items-end p-3 bg-gray-50 rounded-lg"
              >
                <div className="col-span-12 md:col-span-5">
                  <Select
                    label={idx === 0 ? 'الصنف' : undefined}
                    value={line.itemId}
                    onChange={(e) => updateLine(idx, 'itemId', e.target.value)}
                    options={itemOptions}
                    placeholder="اختر صنف"
                  />
                </div>
                <div className="col-span-4 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'الكمية' : undefined}
                    type="number"
                    min={0.01}
                    step={0.01}
                    value={line.quantity}
                    onChange={(e) => updateLine(idx, 'quantity', e.target.value)}
                  />
                </div>
                <div className="col-span-4 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'سعر الوحدة' : undefined}
                    type="number"
                    min={0}
                    step={0.01}
                    value={line.unitPrice}
                    onChange={(e) => updateLine(idx, 'unitPrice', e.target.value)}
                  />
                </div>
                <div className="col-span-3 md:col-span-2">
                  <Input
                    label={idx === 0 ? 'الضريبة %' : undefined}
                    type="number"
                    min={0}
                    step={0.1}
                    value={line.taxRate}
                    onChange={(e) => updateLine(idx, 'taxRate', e.target.value)}
                  />
                </div>
                <div className="col-span-1 flex justify-end">
                  <button
                    type="button"
                    onClick={() => removeLine(idx)}
                    disabled={lines.length === 1}
                    className="text-red-500 hover:text-red-700 p-1 disabled:opacity-30"
                    aria-label="حذف البند"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
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

        <div className="bg-amber-50 border border-amber-200 rounded-lg p-3 text-xs text-amber-800">
          💡 ملاحظة: الـ backend الحالي يوفّر إنشاء أوامر الشراء فقط (لا يوجد PUT لتعديل PO).
          عند الحفظ، سيتم إنشاء أمر جديد بنفس البيانات. الأمر الأصلي سيبقى كما هو.
        </div>

        <div className="flex items-center gap-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
          >
            حفظ التغييرات
          </Button>
          <Link href={`/procurement/purchase-orders/${params.id}`}>
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
