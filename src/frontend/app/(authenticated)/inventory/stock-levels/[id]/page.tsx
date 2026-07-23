'use client';

// تفاصيل مستوى مخزون (Stock Level Detail)

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface StockLevel {
  id: string;
  itemId: string;
  warehouseId: string;
  quantityOnHand: number;
  quantityReserved: number;
  quantityAvailable: number;
  averageCost: number;
  reorderLevel?: number;
}

export default function StockLevelDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<StockLevel | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch(`/api/inventory/levels`, { cache: 'no-store' });
        if (!res.ok) throw new Error('فشل التحميل');
        const all = await res.json();
        const found = all.find((x: StockLevel) => x.id === params.id);
        if (!found) throw new Error('مستوى المخزون غير موجود');
        setItem(found);
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل التحميل'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  if (loading) {
    return (
      <div>
        <PageHeader title="مستوى المخزون" />
        <Card><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card>
      </div>
    );
  }

  if (!item) {
    return (
      <div>
        <PageHeader title="مستوى المخزون" />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'غير موجود'}</div>
          <div className="mt-4"><Link href="/inventory/stock-levels"><Button variant="ghost">رجوع</Button></Link></div>
        </Card>
      </div>
    );
  }

  const isLow = (item.quantityAvailable || 0) <= (item.reorderLevel || 0);

  return (
    <div>
      <PageHeader
        title="📊 مستوى المخزون"
        description={`${item.itemId.substring(0, 8)}... @ ${item.warehouseId.substring(0, 8)}...`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'مستويات المخزون', href: '/inventory/stock-levels' },
          { label: 'تفاصيل' },
        ]}
        actions={
          <Link href="/inventory/stock-levels">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <Card accent="blue">
          <p className="text-gray-500 text-sm">باليد</p>
          <p className="font-bold text-3xl">{Number(item.quantityOnHand).toLocaleString()}</p>
        </Card>
        <Card accent="yellow">
          <p className="text-gray-500 text-sm">محجوز</p>
          <p className="font-bold text-3xl">{Number(item.quantityReserved).toLocaleString()}</p>
        </Card>
        <Card accent="green">
          <p className="text-gray-500 text-sm">متاح</p>
          <p className="font-bold text-3xl">{Number(item.quantityAvailable).toLocaleString()}</p>
        </Card>
      </div>

      <Card className="mt-4">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">الصنف</p>
            <p className="font-mono text-gray-800">{item.itemId}</p>
          </div>
          <div>
            <p className="text-gray-500">المستودع</p>
            <p className="font-mono text-gray-800">{item.warehouseId}</p>
          </div>
          <div>
            <p className="text-gray-500">متوسط التكلفة</p>
            <p className="font-mono text-gray-800">{Number(item.averageCost || 0).toFixed(2)}</p>
          </div>
          <div>
            <p className="text-gray-500">حد إعادة الطلب</p>
            <p className="font-mono text-gray-800">{item.reorderLevel ?? 'غير محدد'}</p>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الحالة</p>
            {isLow ? <Badge variant="danger">⚠️ تحت حد إعادة الطلب - أعد الطلب</Badge> : <Badge variant="success">✅ ضمن الحد الطبيعي</Badge>}
          </div>
        </div>
        <div className="mt-4 pt-4 border-t flex gap-2">
          <Link href={`/inventory/movements/new?itemId=${item.itemId}&warehouseId=${item.warehouseId}&type=receive`}>
            <Button variant="primary">+ استلام جديد</Button>
          </Link>
          <Link href={`/inventory/movements/new?itemId=${item.itemId}&warehouseId=${item.warehouseId}&type=issue`}>
            <Button variant="ghost">- صرف</Button>
          </Link>
        </div>
      </Card>
    </div>
  );
}
