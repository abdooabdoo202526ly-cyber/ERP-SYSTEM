'use client';

// صفحة إنشاء منتج جديد (Item) — form

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { inventoryApi, uomApi, getErrorMessage, Item } from '@/lib/api';

// v1.0.22: ItemType/CostingMethod are numeric enums in the backend.
//   ItemType:        RawMaterial=1, FinishedGood=2, Consumable=3, Service=4
//   CostingMethod:   FIFO=1, LIFO=2, Average=3, Standard=4
const ITEM_TYPES = [
  { label: 'مادة خام (Raw Material)', value: 1 },
  { label: 'منتج تام (Finished Good)', value: 2 },
  { label: 'مستهلك (Consumable)', value: 3 },
  { label: 'خدمة (Service)', value: 4 },
];

const COSTING_METHODS = [
  { label: 'FIFO', value: 1 },
  { label: 'LIFO', value: 2 },
  { label: 'متوسط التكلفة (Average)', value: 3 },
  { label: 'تكلفة قياسية (Standard)', value: 4 },
];

interface FormState {
  sku: string;
  barcode: string;
  name: string;
  description: string;
  itemType: number;
  costingMethod: number;
  unitOfMeasureId: string;
  standardCost: string;
  reorderLevel: string;
  reorderQuantity: string;
}

export default function NewItemPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    sku: '',
    barcode: '',
    name: '',
    description: '',
    itemType: 2,        // FinishedGood (default)
    costingMethod: 3,   // Average (default)
    unitOfMeasureId: '',
    standardCost: '0',
    reorderLevel: '0',
    reorderQuantity: '0',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // v1.0.23: load UoMs for the dropdown
  const [uoms, setUoms] = useState<{ id: string; code: string; name: string; symbol?: string }[]>([]);
  useEffect(() => {
    uomApi.list(true)
      .then((list) => setUoms(list.map((u) => ({ id: u.id, code: u.code, name: u.name, symbol: u.symbol }))))
      .catch(() => setUoms([]));
  }, []);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) => ({
      ...f,
      [k]: ['itemType', 'costingMethod'].includes(k as string) ? Number(v) : v,
    }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      // v1.0.29: trim SKU/Name/Barcode before submit to avoid whitespace-only rejections
      const sku = form.sku.trim();
      const name = form.name.trim();
      const barcode = form.barcode.trim();
      if (!sku) { setError('SKU مطلوب.'); setSubmitting(false); return; }
      if (!name) { setError('اسم المنتج مطلوب.'); setSubmitting(false); return; }
      await inventoryApi.createItem({
        sku,
        barcode: barcode || undefined,
        name,
        description: form.description.trim() || undefined,
        itemType: form.itemType,
        costingMethod: form.costingMethod,
        unitOfMeasureId: form.unitOfMeasureId || undefined,
        standardCost: Number(form.standardCost) || 0,
        reorderLevel: Number(form.reorderLevel) || 0,
        reorderQuantity: Number(form.reorderQuantity) || 0,
      } as Partial<Item>);
      router.push('/inventory/items');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء المنتج. تأكد من البيانات وأن الـ backend يدعم الـ endpoint.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ منتج جديد"
        description="أضف صنفاً جديداً إلى المخزون"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخزون', href: '/inventory/items' },
          { label: 'المنتجات', href: '/inventory/items' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/inventory/items">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للقائمة
            </Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="SKU *"
              value={form.sku}
              onChange={onChange('sku')}
              required
              placeholder="مثال: ITEM-001"
            />
            <Input
              label="الباركود"
              value={form.barcode}
              onChange={onChange('barcode')}
              placeholder="اختياري"
            />
          </div>

          <Input
            label="اسم المنتج *"
            value={form.name}
            onChange={onChange('name')}
            required
            placeholder="مثال: حديد تسليح 12mm"
          />

          <Input
            label="الوصف"
            value={form.description}
            onChange={onChange('description')}
            placeholder="وصف تفصيلي (اختياري)"
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="نوع المنتج *"
              value={form.itemType}
              onChange={onChange('itemType')}
              options={ITEM_TYPES}
            />
            <Select
              label="طريقة التكلفة *"
              value={form.costingMethod}
              onChange={onChange('costingMethod')}
              options={COSTING_METHODS}
            />
          </div>

          <Select
            label="وحدة القياس *"
            value={form.unitOfMeasureId}
            onChange={onChange('unitOfMeasureId')}
            required
            options={[
              { value: '', label: uoms.length === 0 ? 'لا توجد وحدات قياس — أنشئ وحدة أولاً' : 'اختر وحدة القياس' },
              ...uoms.map((u) => ({ value: u.id, label: `${u.code} — ${u.name}${u.symbol ? ` (${u.symbol})` : ''}` })),
            ]}
          />

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input
              label="التكلفة القياسية"
              type="number"
              step="0.01"
              value={form.standardCost}
              onChange={onChange('standardCost')}
            />
            <Input
              label="حد إعادة الطلب"
              type="number"
              value={form.reorderLevel}
              onChange={onChange('reorderLevel')}
            />
            <Input
              label="كمية إعادة الطلب"
              type="number"
              value={form.reorderQuantity}
              onChange={onChange('reorderQuantity')}
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ المنتج
            </Button>
            <Link href="/inventory/items">
              <Button type="button" variant="ghost">
                إلغاء
              </Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
