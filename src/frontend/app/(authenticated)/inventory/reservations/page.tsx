'use client';

// قائمة حجوزات المخزون (Reservations)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye } from 'lucide-react';
import { Card, Badge, PageHeader, Button, EntityActions } from '@/components/ui';
import { api } from '@/lib/api';
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

export default function ReservationsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<StockReservation[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/inventory/reservations', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  const totalQty = items.reduce((s, r) => s + Number(r.quantity || 0), 0);
  const now = Date.now();
  const active = items.filter((r) => new Date(r.expiresAt).getTime() > now).length;
  const expired = items.length - active;

  return (
    <div>
      <PageHeader
        title="🔒 حجوزات المخزون"
        description="حجوزات المخزون المرتبطة بأوامر صرف"
        actions={
          <Link href="/inventory/reservations/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>حجز جديد</Button>
          </Link>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>}

      {!loading && items.length > 0 && (
        <div className="grid grid-cols-3 gap-4 mb-4">
          <Card accent="blue">
            <p className="text-gray-500 text-sm">إجمالي الحجوزات</p>
            <p className="font-bold text-2xl">{items.length}</p>
          </Card>
          <Card accent="green">
            <p className="text-gray-500 text-sm">فعّال</p>
            <p className="font-bold text-2xl">{active}</p>
          </Card>
          <Card accent="red">
            <p className="text-gray-500 text-sm">منتهي الصلاحية</p>
            <p className="font-bold text-2xl">{expired}</p>
          </Card>
        </div>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">لا توجد حجوزات.</div>
      ) : (
        <div className="space-y-2">
          {items.map((r) => {
            const isExpired = new Date(r.expiresAt).getTime() < now;
            return (
              <Card key={r.id} accent={isExpired ? 'gray' : 'yellow'}>
                <div className="flex items-start justify-between">
                  <div>
                    <p className="text-xs text-gray-500 font-mono">{r.referenceType} #{r.referenceId.substring(0, 8)}</p>
                    <h3 className="font-bold text-gray-800 mt-1">
                      <span className="font-mono text-xl">{Number(r.quantity).toLocaleString()}</span>
                      <span className="text-sm text-gray-500 ms-2">وحدة محجوزة</span>
                    </h3>
                    <div className="flex items-center gap-3 mt-2 text-xs text-gray-500">
                      <span>📦 {r.itemId.substring(0, 8)}...</span>
                      <span>🏢 {r.warehouseId.substring(0, 8)}...</span>
                      <span>⏰ ينتهي: {new Date(r.expiresAt).toLocaleDateString('en-GB')}</span>
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <Link href={`/inventory/reservations/${r.id}`}>
                      <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3 w-3" />} />
                    </Link>
                    <EntityActions
                      itemLabel={`حجز ${r.referenceType}`}
                      hideEdit
                      onDelete={async () => {
                        try {
                          await api.delete(`/api/inventory/reservations/${r.id}`);
                          await load();
                        } catch (e) {
                          setError(getErrorMessage(e, 'فشل حذف الحجز.'));
                        }
                      }}
                    />
                    <Badge variant={isExpired ? 'danger' : 'success'}>{isExpired ? 'منتهي' : 'فعّال'}</Badge>
                  </div>
                </div>
              </Card>
            );
          })}
          <p className="mt-3 text-xs text-gray-500">إجمالي الكمية المحجوزة: {totalQty.toLocaleString()}</p>
        </div>
      )}
    </div>
  );
}
