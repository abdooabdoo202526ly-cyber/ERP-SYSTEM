'use client';

// إنشاء حجز مخزون جديد (Reservation)

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

const REF_TYPES = [
  { label: 'أمر بيع (SalesOrder)', value: 'SalesOrder' },
  { label: 'أمر شراء (PurchaseOrder)', value: 'PurchaseOrder' },
  { label: 'أمر عمل (WorkOrder)', value: 'WorkOrder' },
  { label: 'تحويل (Transfer)', value: 'Transfer' },
];

interface FormState {
  itemId: string;
  warehouseId: string;
  quantity: string;
  referenceType: string;
  referenceId: string;
  expiresAt: string;
}

export default function NewReservationPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    itemId: '',
    warehouseId: '',
    quantity: '1',
    referenceType: 'SalesOrder',
    referenceId: '',
    expiresAt: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString().slice(0, 10),
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/api/inventory/reservations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          itemId: form.itemId,
          warehouseId: form.warehouseId,
          quantity: Number(form.quantity),
          referenceType: form.referenceType,
          referenceId: form.referenceId,
          expiresAt: form.expiresAt,
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء الحجز');
      }
      router.push('/inventory/reservations');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الحجز.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ حجز مخزون جديد"
        description="احجز كمية من صنف لأمر محدد"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الحجوزات', href: '/inventory/reservations' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/inventory/reservations">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="الصنف (Item ID) *" value={form.itemId} onChange={onChange('itemId')} required placeholder="UUID" />
            <Input label="المستودع *" value={form.warehouseId} onChange={onChange('warehouseId')} required placeholder="UUID" />
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="الكمية *" type="number" step="0.01" value={form.quantity} onChange={onChange('quantity')} required />
            <Input label="تاريخ الانتهاء *" type="date" value={form.expiresAt} onChange={onChange('expiresAt')} required />
          </div>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select label="نوع المرجع *" value={form.referenceType} onChange={onChange('referenceType')} options={REF_TYPES} />
            <Input label="معرّف المرجع *" value={form.referenceId} onChange={onChange('referenceId')} required placeholder="UUID" />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>حفظ</Button>
            <Link href="/inventory/reservations">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
