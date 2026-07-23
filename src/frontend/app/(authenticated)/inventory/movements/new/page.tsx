'use client';

// إنشاء حركة مخزون جديدة (Stock Movement)

import { useEffect, useState } from 'react';
import { Suspense } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

const MOVEMENT_TYPES = [
  { label: 'استلام (Receive)', value: 'receive' },
  { label: 'صرف (Issue)', value: 'issue' },
  { label: 'تسوية (Adjustment)', value: 'adjust' },
  { label: 'تحويل (Transfer)', value: 'transfer' },
];

interface FormState {
  type: string;
  reference: string;
  movementDate: string;
  itemId: string;
  warehouseId: string;
  destinationWarehouseId: string;
  quantity: string;
  unitCost: string;
  projectId: string;
  costCenterId: string;
  notes: string;
  companyId: string;
}

function NewMovementForm() {
  const router = useRouter();
  const sp = useSearchParams();
  useAuth();
  const [form, setForm] = useState<FormState>({
    type: sp.get('type') || 'receive',
    reference: `MV-${Date.now().toString().slice(-6)}`,
    movementDate: new Date().toISOString().split('T')[0],
    itemId: sp.get('itemId') || '',
    warehouseId: sp.get('warehouseId') || '',
    destinationWarehouseId: '',
    quantity: '0',
    unitCost: '0',
    projectId: '',
    costCenterId: '',
    notes: '',
    companyId: '',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>
  ) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);

    const payload = {
      companyId: form.companyId || '00000000-0000-0000-0000-000000000001',
      reference: form.reference,
      movementDate: form.movementDate,
      itemId: form.itemId,
      warehouseId: form.warehouseId,
      destinationWarehouseId: form.type === 'transfer' ? form.destinationWarehouseId : undefined,
      quantity: Number(form.quantity),
      unitCost: Number(form.unitCost) || 0,
      projectId: form.projectId || null,
      costCenterId: form.costCenterId || null,
      notes: form.notes || null,
    };

    const endpoint = `/api/inventory/movements/${form.type}`;

    try {
      const res = await fetch(endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء الحركة');
      }
      router.push(`/inventory/movements`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الحركة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ حركة مخزون جديدة"
        description="استلام / صرف / تحويل / تسوية"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'حركات المخزون', href: '/inventory/movements' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/inventory/movements">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-3xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Select label="نوع الحركة *" value={form.type} onChange={onChange('type')} options={MOVEMENT_TYPES} />
            <Input label="التاريخ *" type="date" value={form.movementDate} onChange={onChange('movementDate')} required />
            <Input label="المرجع *" value={form.reference} onChange={onChange('reference')} required />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="الصنف (Item ID) *" value={form.itemId} onChange={onChange('itemId')} required placeholder="UUID" />
            <Input label="المستودع (Warehouse ID) *" value={form.warehouseId} onChange={onChange('warehouseId')} required placeholder="UUID" />
          </div>

          {form.type === 'transfer' && (
            <Input label="المستودع الوجهة *" value={form.destinationWarehouseId} onChange={onChange('destinationWarehouseId')} required placeholder="UUID" />
          )}

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input label="الكمية *" type="number" step="0.01" value={form.quantity} onChange={onChange('quantity')} required />
            {form.type !== 'issue' && (
              <Input label="سعر الوحدة" type="number" step="0.01" value={form.unitCost} onChange={onChange('unitCost')} />
            )}
            <Input label="الشركة (Company ID) *" value={form.companyId} onChange={onChange('companyId')} required placeholder="UUID" />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="المشروع (Project ID)" value={form.projectId} onChange={onChange('projectId')} placeholder="اختياري" />
            <Input label="مركز التكلفة" value={form.costCenterId} onChange={onChange('costCenterId')} placeholder="اختياري" />
          </div>

          <div>
            <label className="block text-sm text-gray-500 mb-1">ملاحظات</label>
            <textarea
              value={form.notes}
              onChange={onChange('notes')}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm"
              rows={2}
              placeholder="اختياري"
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>
              حفظ الحركة
            </Button>
            <Link href="/inventory/movements">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}

export default function NewStockMovementPage() {
  return (
    <Suspense fallback={<div>جاري التحميل...</div>}>
      <NewMovementForm />
    </Suspense>
  );
}
