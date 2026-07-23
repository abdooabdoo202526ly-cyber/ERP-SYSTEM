'use client';

// صفحة تعديل المنتج (Item) — form

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { inventoryApi, uomApi, getErrorMessage } from '@/lib/api';

// v1.0.23: numeric enums to match backend ItemType/CostingMethod
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
  isActive: boolean;
}

export default function EditItemPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [form, setForm] = useState<FormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // v1.0.23: load UoMs for the dropdown
  const [uoms, setUoms] = useState<{ id: string; code: string; name: string; symbol?: string }[]>([]);
  useEffect(() => {
    uomApi.list(true)
      .then((list) => setUoms(list.map((u) => ({ id: u.id, code: u.code, name: u.name, symbol: u.symbol }))))
      .catch(() => setUoms([]));
  }, []);

  useEffect(() => {
    const load = async () => {
      try {
        const data = await inventoryApi.getItem(params.id);
        setForm({
          sku: data.sku || '',
          barcode: data.barcode || '',
          name: data.name || '',
          description: data.description || '',
          itemType: data.itemType || 2,
          costingMethod: data.costingMethod || 3,
          unitOfMeasureId: data.unitOfMeasureId || '',
          standardCost: String(data.standardCost ?? data.averageCost ?? 0),
          reorderLevel: String(data.reorderLevel ?? 0),
          reorderQuantity: String(data.reorderQuantity ?? 0),
          isActive: data.isActive ?? true,
        });
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل تحميل المنتج'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const v = e.target.value;
    setForm((f) =>
      f ? { ...f, [k]: ['itemType', 'costingMethod'].includes(k as string) ? Number(v) : v } : f
    );
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setError(null);
    setSubmitting(true);
    try {
      await inventoryApi.updateItem(params.id, {
        sku: form.sku,
        barcode: form.barcode || undefined,
        name: form.name,
        description: form.description || undefined,
        itemType: form.itemType,
        costingMethod: form.costingMethod,
        unitOfMeasureId: form.unitOfMeasureId || undefined,
        standardCost: Number(form.standardCost) || 0,
        reorderLevel: Number(form.reorderLevel) || 0,
        reorderQuantity: Number(form.reorderQuantity) || 0,
        isActive: form.isActive,
      });
      router.push('/inventory/items');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل حفظ التعديلات.'));
    } finally {
      setSubmitting(false);
    }
  };

  if (loading || !form) {
    return (
      <div className="p-12 text-center text-gray-500">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
        <p className="mt-3 text-sm">جاري تحميل المنتج...</p>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل منتج"
        description={form.name}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخزون', href: '/inventory/items' },
          { label: 'المنتجات', href: '/inventory/items' },
          { label: 'تعديل' },
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
            <Input label="SKU *" value={form.sku} onChange={onChange('sku')} required />
            <Input label="الباركود" value={form.barcode} onChange={onChange('barcode')} />
          </div>

          <Input label="اسم المنتج *" value={form.name} onChange={onChange('name')} required />

          <Input label="الوصف" value={form.description} onChange={onChange('description')} />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select label="نوع المنتج" value={String(form.itemType)} onChange={onChange('itemType')} options={ITEM_TYPES} />
            <Select label="طريقة التكلفة" value={String(form.costingMethod)} onChange={onChange('costingMethod')} options={COSTING_METHODS} />
          </div>

          <Select
            label="وحدة القياس *"
            value={form.unitOfMeasureId}
            onChange={onChange('unitOfMeasureId')}
            required
            options={[
              { value: '', label: uoms.length === 0 ? 'لا توجد وحدات قياس' : 'اختر وحدة القياس' },
              ...uoms.map((u) => ({ value: u.id, label: `${u.code} — ${u.name}${u.symbol ? ` (${u.symbol})` : ''}` })),
            ]}
          />

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input label="التكلفة القياسية" type="number" step="0.01" value={form.standardCost} onChange={onChange('standardCost')} />
            <Input label="حد إعادة الطلب" type="number" value={form.reorderLevel} onChange={onChange('reorderLevel')} />
            <Input label="كمية إعادة الطلب" type="number" value={form.reorderQuantity} onChange={onChange('reorderQuantity')} />
          </div>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
              className="rounded border-gray-300"
            />
            <span>فعّال</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
              حفظ التعديلات
            </Button>
            <Link href="/inventory/items">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
