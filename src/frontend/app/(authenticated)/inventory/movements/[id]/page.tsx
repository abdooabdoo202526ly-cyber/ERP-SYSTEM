'use client';

// تفاصيل حركة مخزون (Stock Movement Detail)

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Send } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface StockMovement {
  id: string;
  reference: string;
  type: number;
  status: number;
  movementDate: string;
  itemId: string;
  warehouseId: string;
  destinationWarehouseId?: string;
  quantity: number;
  unitCost: number;
  totalCost: number;
  projectId?: string;
  costCenterId?: string;
  notes?: string;
  createdAt: string;
  postedAt?: string;
}

const TYPES: Record<number, string> = { 1: 'استلام', 2: 'صرف', 3: 'تحويل', 4: 'تسوية' };
const STATUSES: Record<number, { label: string; variant: 'success' | 'warning' | 'neutral' }> = {
  1: { label: 'مسودة', variant: 'warning' },
  2: { label: 'مُرحَّل', variant: 'success' },
  3: { label: 'معكوس', variant: 'neutral' },
};

export default function StockMovementDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<StockMovement | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionRunning, setActionRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      const res = await fetch(`/api/inventory/movements/${params.id}`, { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItem(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [params.id]);

  const handlePost = async () => {
    if (!item) return;
    if (!confirm('ترحيل الحركة؟')) return;
    setActionRunning(true);
    try {
      const res = await fetch(`/api/inventory/movements/${item.id}/post`, { method: 'POST' });
      if (!res.ok) throw new Error('فشل الترحيل');
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الترحيل.'));
    } finally {
      setActionRunning(false);
    }
  };

  if (loading) return <div><PageHeader title="حركة" /><Card><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card></div>;
  if (!item) return <div><PageHeader title="حركة" /><Card><div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'غير موجود'}</div><div className="mt-4"><Link href="/inventory/movements"><Button variant="ghost">رجوع</Button></Link></div></Card></div>;

  const status = STATUSES[item.status] || { label: `Status ${item.status}`, variant: 'neutral' as const };

  return (
    <div>
      <PageHeader
        title="🔄 حركة مخزون"
        description={item.reference}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'حركات المخزون', href: '/inventory/movements' },
          { label: item.reference },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {item.status === 1 && (
              <Button variant="primary" onClick={handlePost} loading={actionRunning} iconLeft={<Send className="h-4 w-4" />}>ترحيل</Button>
            )}
            <Link href="/inventory/movements">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
          </div>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

      <Card className="max-w-3xl">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <p className="text-gray-500">المرجع</p>
            <p className="font-mono text-blue-600">{item.reference}</p>
          </div>
          <div>
            <p className="text-gray-500">النوع</p>
            <Badge variant="info">{TYPES[item.type] || item.type}</Badge>
          </div>
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={status.variant}>{status.label}</Badge>
          </div>
          <div>
            <p className="text-gray-500">التاريخ</p>
            <p className="font-mono">{new Date(item.movementDate).toLocaleDateString('en-GB')}</p>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الصنف</p>
            <p className="font-mono text-xs">{item.itemId}</p>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">المستودع</p>
            <p className="font-mono text-xs">{item.warehouseId}</p>
          </div>
          {item.destinationWarehouseId && (
            <div className="col-span-2">
              <p className="text-gray-500">المستودع الوجهة</p>
              <p className="font-mono text-xs">{item.destinationWarehouseId}</p>
            </div>
          )}
          <div>
            <p className="text-gray-500">الكمية</p>
            <p className="font-bold text-2xl font-mono">{Number(item.quantity).toLocaleString()}</p>
          </div>
          <div>
            <p className="text-gray-500">سعر الوحدة</p>
            <p className="font-mono text-lg">{Number(item.unitCost || 0).toFixed(2)}</p>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الإجمالي</p>
            <p className="font-bold text-xl font-mono text-blue-600">{Number(item.totalCost).toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
          </div>
          {item.projectId && (
            <div>
              <p className="text-gray-500">المشروع</p>
              <p className="font-mono text-xs">{item.projectId}</p>
            </div>
          )}
          {item.costCenterId && (
            <div>
              <p className="text-gray-500">مركز التكلفة</p>
              <p className="font-mono text-xs">{item.costCenterId}</p>
            </div>
          )}
          {item.postedAt && (
            <div className="col-span-2">
              <p className="text-gray-500">تاريخ الترحيل</p>
              <p className="font-mono">{new Date(item.postedAt).toLocaleString('en-GB')}</p>
            </div>
          )}
          {item.notes && (
            <div className="col-span-2">
              <p className="text-gray-500">ملاحظات</p>
              <p>{item.notes}</p>
            </div>
          )}
        </div>
      </Card>
    </div>
  );
}
