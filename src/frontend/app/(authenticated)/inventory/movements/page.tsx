'use client';

// قائمة حركات المخزون (Stock Movements)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, ArrowRight } from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface StockMovement {
  id: string;
  reference: string;
  type: number; // 1=Receive, 2=Issue, 3=Transfer, 4=Adjustment
  status: number; // 1=Draft, 2=Posted, 3=Reversed
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

const MOVEMENT_TYPES: Record<number, { label: string; variant: 'success' | 'warning' | 'info' |  'neutral' }> = {
  1: { label: 'استلام (Receive)', variant: 'success' },
  2: { label: 'صرف (Issue)', variant: 'warning' },
  3: { label: 'تحويل (Transfer)', variant: 'info' },
  4: { label: 'تسوية (Adjustment)', variant:  'neutral' },
};

const MOVEMENT_STATUSES: Record<number, string> = {
  1: 'مسودة',
  2: 'مُرحَّل',
  3: 'معكوس',
};

export default function StockMovementsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<StockMovement[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [typeFilter, setTypeFilter] = useState<string>('all');

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/inventory/movements', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = typeFilter === 'all' ? items : items.filter((m) => String(m.type) === typeFilter);

  const totalCost = items.reduce((s, m) => s + Number(m.totalCost || 0), 0);

  return (
    <div>
      <PageHeader
        title="🔄 حركات المخزون"
        description="سجل حركات المخزون (استلام / صرف / تحويل / تسوية)"
        actions={
          <div className="flex items-center gap-2">
            <select
              value={typeFilter}
              onChange={(e) => setTypeFilter(e.target.value)}
              className="border border-gray-300 rounded px-3 py-2 text-sm"
            >
              <option value="all">جميع الأنواع</option>
              <option value="1">استلام</option>
              <option value="2">صرف</option>
              <option value="3">تحويل</option>
              <option value="4">تسوية</option>
            </select>
            <Link href="/inventory/movements/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>حركة جديدة</Button>
            </Link>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>
      )}

      {!loading && items.length > 0 && (
        <Card className="mb-4">
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-500">إجمالي الحركات: {items.length}</span>
            <span className="text-gray-500">إجمالي القيمة: <span className="font-bold font-mono">{totalCost.toLocaleString(undefined, { minimumFractionDigits: 2 })}</span></span>
          </div>
        </Card>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          لا توجد حركات مخزون.
        </div>
      ) : (
        <div className="space-y-2">
          {filtered.map((m) => {
            const t = MOVEMENT_TYPES[m.type] || { label: `Type ${m.type}`, variant: 'neutral' as const };
            return (
              <Card key={m.id}>
                <div className="flex items-start justify-between">
                  <div>
                    <p className="text-xs text-gray-500 font-mono">{m.reference}</p>
                    <h3 className="font-bold text-gray-800 mt-1">
                      {Number(m.quantity).toLocaleString()} × {Number(m.unitCost || 0).toFixed(2)}
                      <span className="text-gray-500 text-sm font-normal ms-2">
                        = <span className="font-mono font-bold">{Number(m.totalCost).toFixed(2)}</span>
                      </span>
                    </h3>
                    <div className="flex items-center gap-3 mt-2 text-xs text-gray-500">
                      <span>📅 {new Date(m.movementDate).toLocaleDateString('en-GB')}</span>
                      <span>📦 {m.itemId.substring(0, 8)}...</span>
                      <span>🏢 {m.warehouseId.substring(0, 8)}...</span>
                      {m.destinationWarehouseId && (
                        <span className="flex items-center">
                          <ArrowRight className="h-3 w-3 mx-1" />
                          {m.destinationWarehouseId.substring(0, 8)}...
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <Link href={`/inventory/movements/${m.id}`}>
                      <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3 w-3" />} />
                    </Link>
                    <Badge variant={t.variant}>{t.label}</Badge>
                    <Badge variant={m.status === 2 ? 'success' : m.status === 3 ? 'neutral' : 'warning'}>
                      {MOVEMENT_STATUSES[m.status] || `Status ${m.status}`}
                    </Badge>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
