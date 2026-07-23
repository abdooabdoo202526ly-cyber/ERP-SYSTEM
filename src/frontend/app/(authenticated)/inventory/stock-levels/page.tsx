'use client';

// قائمة مستويات المخزون (Stock Levels)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Eye, AlertTriangle } from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
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

export default function StockLevelsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<StockLevel[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');
  const [showLowStockOnly, setShowLowStockOnly] = useState(false);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async (lowStock = false) => {
    setLoading(true);
    setError(null);
    try {
      const url = lowStock ? '/api/inventory/levels/low-stock' : '/api/inventory/levels';
      const res = await fetch(url, { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  const toggleLowStock = async () => {
    const newVal = !showLowStockOnly;
    setShowLowStockOnly(newVal);
    await load(newVal);
  };

  const totalOnHand = items.reduce((s, i) => s + Number(i.quantityOnHand || 0), 0);
  const totalReserved = items.reduce((s, i) => s + Number(i.quantityReserved || 0), 0);
  const totalAvailable = items.reduce((s, i) => s + Number(i.quantityAvailable || 0), 0);

  const filtered = items.filter(
    (i) => !filter || i.itemId.includes(filter) || i.warehouseId.includes(filter)
  );

  return (
    <div>
      <PageHeader
        title="📊 مستويات المخزون"
        description="كميات الأصناف في المستودعات"
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant={showLowStockOnly ? 'primary' : 'ghost'}
              size="sm"
              onClick={toggleLowStock}
              iconLeft={<AlertTriangle className="h-4 w-4" />}
            >
              {showLowStockOnly ? 'عرض الكل' : 'مخزون منخفض'}
            </Button>
            <Input
              placeholder="🔍 بحث..."
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              containerClassName="w-64"
            />
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>
      )}

      {/* Summary */}
      {!loading && items.length > 0 && (
        <div className="grid grid-cols-3 gap-4 mb-4">
          <Card accent="blue">
            <p className="text-gray-500 text-sm">إجمالي المتاح باليد</p>
            <p className="font-bold text-2xl">{totalOnHand.toLocaleString()}</p>
          </Card>
          <Card accent="yellow">
            <p className="text-gray-500 text-sm">محجوز</p>
            <p className="font-bold text-2xl">{totalReserved.toLocaleString()}</p>
          </Card>
          <Card accent="green">
            <p className="text-gray-500 text-sm">متاح للصرف</p>
            <p className="font-bold text-2xl">{totalAvailable.toLocaleString()}</p>
          </Card>
        </div>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          {showLowStockOnly ? 'لا توجد أصناف تحت حد إعادة الطلب.' : 'لا توجد مستويات مخزون.'}
        </div>
      ) : (
        <div className="space-y-2">
          {filtered.map((sl) => {
            const isLow = (sl.quantityAvailable || 0) <= (sl.reorderLevel || 0);
            return (
              <Card key={sl.id} accent={isLow ? 'red' : 'green'}>
                <div className="flex items-start justify-between">
                  <div className="grid grid-cols-4 gap-4 flex-1">
                    <div>
                      <p className="text-xs text-gray-500">الصنف</p>
                      <p className="font-mono text-xs">{sl.itemId.substring(0, 8)}...</p>
                    </div>
                    <div className="text-center">
                      <p className="text-xs text-gray-500">باليد</p>
                      <p className="font-bold text-lg">{Number(sl.quantityOnHand).toLocaleString()}</p>
                    </div>
                    <div className="text-center">
                      <p className="text-xs text-gray-500">محجوز</p>
                      <p className="font-bold text-lg text-yellow-600">{Number(sl.quantityReserved).toLocaleString()}</p>
                    </div>
                    <div className="text-center">
                      <p className="text-xs text-gray-500">متاح</p>
                      <p className="font-bold text-lg text-green-600">{Number(sl.quantityAvailable).toLocaleString()}</p>
                    </div>
                  </div>
                  <Link href={`/inventory/stock-levels/${sl.id}`}>
                    <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3 w-3" />} />
                  </Link>
                </div>
                <div className="mt-2 pt-2 border-t flex items-center justify-between text-xs">
                  <span className="text-gray-500">المستودع: <span className="font-mono">{sl.warehouseId.substring(0, 8)}...</span></span>
                  <span className="text-gray-500">متوسط التكلفة: <span className="font-mono">{Number(sl.averageCost || 0).toFixed(2)}</span></span>
                  {isLow && <Badge variant="danger">⚠️ تحت حد الطلب</Badge>}
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
