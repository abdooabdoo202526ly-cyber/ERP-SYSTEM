'use client';

// صفحة إنشاء استلام بضاعة جديد (Goods Receipt) — يختار PO ويعرض الـ lines

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Select, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  procurementApi,
  inventoryApi,
  PurchaseOrder,
  GoodsReceiptLine,
  PO_STATUSES,
  getErrorMessage,
} from '@/lib/api';

interface LineDraft {
  itemId: string;
  quantity: number;
  notes: string;
}

export default function NewGoodsReceiptPage() {
  const router = useRouter();
  useAuth();
  const [pos, setPOs] = useState<PurchaseOrder[]>([]);
  const [warehouses, setWarehouses] = useState<{ id: string; name: string }[]>([]);
  const [poId, setPoId] = useState('');
  const [warehouseId, setWarehouseId] = useState('');
  const [receivedDate, setReceivedDate] = useState(new Date().toISOString().slice(0, 10));
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
      // نحاول جلب الـ warehouses (إن وُجدت)
      try {
        const wRes = await fetch('/api/inventory/warehouses', {
          headers: { Authorization: `Bearer ${localStorage.getItem('accessToken')}` },
        });
        if (wRes.ok) {
          const data = await wRes.json();
          if (Array.isArray(data)) setWarehouses(data);
        }
      } catch {
        // ignore
      }
      const posData = await procurementApi.listPOs();
      // v1.0.31: نعرض كل الـ POs ما عدا المُستلَم/الملغي (لإعطاء الـ user مرونة)
      // الـ backend سيتحقق من الـ status الفعلي عند الإنشاء
      const openPOs = posData.filter((p) => ![5, 6].includes(p.status));
      setPOs(openPOs);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل البيانات.'));
    } finally {
      setLoading(false);
    }
  };

  const poOptions = pos.map((p) => ({
    label: `${p.poNumber} — ${p.vendorName || ''} (${PO_STATUSES[p.status]})`,
    value: p.id,
  }));

  const warehouseOptions = [
    { label: '— اختر المستودع —', value: '' },
    ...warehouses.map((w) => ({ label: w.name, value: w.id })),
  ];

  // عند اختيار PO، نملأ الـ lines من PO lines
  useEffect(() => {
    if (!poId) {
      setLines([]);
      return;
    }
    const po = pos.find((p) => p.id === poId);
    if (po && po.lines) {
      setLines(
        po.lines.map((l) => ({
          itemId: l.itemId,
          quantity: l.quantity,
          notes: '',
        }))
      );
    }
  }, [poId, pos]);

  const updateLine = (idx: number, key: keyof LineDraft, value: string | number) => {
    setLines((prev) =>
      prev.map((l, i) => (i === idx ? { ...l, [key]: typeof value === 'string' && key === 'quantity' ? Number(value) : value } : l))
    );
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    if (!poId) {
      setError('يجب اختيار أمر الشراء.');
      return;
    }
    if (!warehouseId) {
      setError('يجب اختيار المستودع.');
      return;
    }
    if (lines.length === 0) {
      setError('أمر الشراء هذا لا يحتوي على بنود.');
      return;
    }

    setSubmitting(true);
    try {
      const linesDto: GoodsReceiptLine[] = lines.map((l, i) => ({
        id: `temp-${i}`,
        itemId: l.itemId,
        quantity: l.quantity,
        notes: l.notes || undefined,
      }));
      await procurementApi.createGR({
        purchaseOrderId: poId,
        warehouseId,
        receivedDate,
        lines: linesDto,
      } as Partial<{ purchaseOrderId: string; warehouseId: string; receivedDate: string; lines: GoodsReceiptLine[] }>);
      router.push('/procurement/goods-receipts');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء سند الاستلام.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ استلام بضاعة جديد"
        description="إنشاء Goods Receipt (GR) من أمر شراء"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'استلامات البضاعة', href: '/procurement/goods-receipts' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/procurement/goods-receipts">
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

        <Card title="معلومات الاستلام">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Select
              label="أمر الشراء *"
              value={poId}
              onChange={(e) => setPoId(e.target.value)}
              options={poOptions}
              placeholder={loading ? 'جاري التحميل...' : 'اختر PO'}
              required
            />
            <Select
              label="المستودع *"
              value={warehouseId}
              onChange={(e) => setWarehouseId(e.target.value)}
              options={warehouseOptions}
              required
            />
            <Input
              type="date"
              label="تاريخ الاستلام"
              value={receivedDate}
              onChange={(e) => setReceivedDate(e.target.value)}
              required
            />
          </div>
        </Card>

        {lines.length > 0 && (
          <Card title="بنود الاستلام (مأخوذة من PO)">
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
                      label={idx === 0 ? 'الكمية المُستلمة' : undefined}
                      type="number"
                      min={0.01}
                      step={0.01}
                      value={line.quantity}
                      onChange={(e) => updateLine(idx, 'quantity', e.target.value)}
                    />
                  </div>
                  <div className="col-span-6 md:col-span-5">
                    <Input
                      label={idx === 0 ? 'ملاحظات' : undefined}
                      value={line.notes}
                      onChange={(e) => updateLine(idx, 'notes', e.target.value)}
                      placeholder="اختياري"
                    />
                  </div>
                </div>
              ))}
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
            حفظ الاستلام
          </Button>
          <Link href="/procurement/goods-receipts">
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
