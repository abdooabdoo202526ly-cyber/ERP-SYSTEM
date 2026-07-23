'use client';

// تفاصيل حجز مخزون + إلغاء (Reservation Detail with Cancel)

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, X } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface StockReservation {
  id: string;
  itemId: string;
  warehouseId: string;
  quantity: number;
  referenceType: string;
  referenceId: string;
  expiresAt: string;
  createdAt: string;
}

export default function ReservationDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<StockReservation | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionRunning, setActionRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      const res = await fetch('/api/inventory/reservations', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const all = await res.json();
      const found = all.find((x: StockReservation) => x.id === params.id);
      if (!found) throw new Error('الحجز غير موجود');
      setItem(found);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [params.id]);

  const handleCancel = async () => {
    if (!item) return;
    if (!confirm('إلغاء هذا الحجز؟')) return;
    setActionRunning(true);
    try {
      const res = await fetch(`/api/inventory/reservations/${item.id}`, { method: 'DELETE' });
      if (!res.ok && res.status !== 204) throw new Error('فشل الإلغاء');
      router.push('/inventory/reservations');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الإلغاء.'));
      setActionRunning(false);
    }
  };

  if (loading) return <div><PageHeader title="حجز" /><Card><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card></div>;
  if (!item) return <div><PageHeader title="حجز" /><Card><div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'غير موجود'}</div><div className="mt-4"><Link href="/inventory/reservations"><Button variant="ghost">رجوع</Button></Link></div></Card></div>;

  const isExpired = new Date(item.expiresAt).getTime() < Date.now();

  return (
    <div>
      <PageHeader
        title="🔒 حجز مخزون"
        description={`${item.referenceType} → ${item.referenceId.substring(0, 8)}...`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الحجوزات', href: '/inventory/reservations' },
          { label: 'تفاصيل' },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {!isExpired && (
              <Button variant="danger" onClick={handleCancel} loading={actionRunning} iconLeft={<X className="h-4 w-4" />}>
                إلغاء الحجز
              </Button>
            )}
            <Link href="/inventory/reservations">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
          </div>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

      <Card className="max-w-3xl">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">الكمية المحجوزة</p>
            <p className="font-bold text-3xl font-mono">{Number(item.quantity).toLocaleString()}</p>
          </div>
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={isExpired ? 'danger' : 'success'}>{isExpired ? 'منتهي الصلاحية' : 'فعّال'}</Badge>
          </div>
          <div>
            <p className="text-gray-500">الصنف</p>
            <p className="font-mono text-xs">{item.itemId}</p>
          </div>
          <div>
            <p className="text-gray-500">المستودع</p>
            <p className="font-mono text-xs">{item.warehouseId}</p>
          </div>
          <div>
            <p className="text-gray-500">نوع المرجع</p>
            <Badge variant="info">{item.referenceType}</Badge>
          </div>
          <div>
            <p className="text-gray-500">معرّف المرجع</p>
            <p className="font-mono text-xs">{item.referenceId}</p>
          </div>
          <div>
            <p className="text-gray-500">تاريخ الانتهاء</p>
            <p className="font-mono">{new Date(item.expiresAt).toLocaleString('en-GB')}</p>
          </div>
          <div>
            <p className="text-gray-500">تاريخ الإنشاء</p>
            <p className="font-mono">{new Date(item.createdAt).toLocaleString('en-GB')}</p>
          </div>
        </div>
      </Card>
    </div>
  );
}
