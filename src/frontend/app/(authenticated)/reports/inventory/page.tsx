'use client';

// صفحة تقارير المخزون — أربعة تبويبات:
//   1) تقييم المخزون (Stock Valuation)
//   2) حركات المخزون (Movement History)
//   3) تحت حد الطلب (Low Stock)
//   4) أعمار المخزون (Stock Aging)

import { useEffect, useState, useCallback } from 'react';
import { Card, PageHeader, Input, Table, Badge, Button } from '@/components/ui';
import {
  reportsApi,
  StockValuationResponse,
  StockMovementHistoryResponse,
  LowStockResponse,
  StockAgingResponse,
  STOCK_MOVEMENT_TYPE_LABELS,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber, formatMoney } from '@/lib/format';
import { RefreshCcw } from 'lucide-react';

type TabKey = 'valuation' | 'movements' | 'low-stock' | 'aging';

const TABS: { key: TabKey; label: string; icon: string }[] = [
  { key: 'valuation', label: 'تقييم المخزون', icon: '💰' },
  { key: 'movements', label: 'حركات المخزون', icon: '↔️' },
  { key: 'low-stock', label: 'تحت حد الطلب', icon: '⚠️' },
  { key: 'aging', label: 'أعمار المخزون', icon: '📅' },
];

const today = (): string => new Date().toISOString().slice(0, 10);
const monthAgo = (): string => {
  const d = new Date();
  d.setMonth(d.getMonth() - 1);
  return d.toISOString().slice(0, 10);
};

const MOVEMENT_TYPE_VARIANT: Record<string, 'success' | 'danger' | 'info' | 'warning' | 'neutral'> = {
  Receive: 'success',
  Return: 'success',
  Issue: 'danger',
  Transfer: 'info',
  Adjust: 'warning',
};

const LOW_STOCK_STATUS_VARIANT: Record<string, 'danger' | 'warning' | 'info'> = {
  Critical: 'danger',
  Warning: 'warning',
  Low: 'info',
};

const LOW_STOCK_STATUS_LABEL: Record<string, string> = {
  Critical: 'حرج (لا يوجد)',
  Warning: 'تحذير',
  Low: 'منخفض',
};

const AGING_BUCKET_VARIANT: Record<string, 'success' | 'info' | 'warning' | 'danger' | 'neutral'> = {
  '': 'neutral',
  '0-30': 'success',
  '31-60': 'info',
  '61-90': 'warning',
  '90+': 'danger',
};

export default function InventoryReportsPage() {
  const [tab, setTab] = useState<TabKey>('valuation');

  // Filters
  const [fromDate, setFromDate] = useState<string>(monthAgo());
  const [toDate, setToDate] = useState<string>(today());
  const [itemFilter, setItemFilter] = useState<string>('');

  // Data
  const [valuation, setValuation] = useState<StockValuationResponse | null>(null);
  const [movements, setMovements] = useState<StockMovementHistoryResponse | null>(null);
  const [lowStock, setLowStock] = useState<LowStockResponse | null>(null);
  const [aging, setAging] = useState<StockAgingResponse | null>(null);

  // Loading & error per tab
  const [loading, setLoading] = useState<Record<TabKey, boolean>>({
    valuation: false,
    movements: false,
    'low-stock': false,
    aging: false,
  });
  const [error, setError] = useState<Record<TabKey, string | null>>({
    valuation: null,
    movements: null,
    'low-stock': null,
    aging: null,
  });

  const loadValuation = useCallback(async () => {
    setLoading((s) => ({ ...s, valuation: true }));
    setError((s) => ({ ...s, valuation: null }));
    try {
      const data = await reportsApi.inventoryValuation();
      setValuation(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, valuation: getErrorMessage(e, 'تعذّر تحميل تقييم المخزون.') }));
    } finally {
      setLoading((s) => ({ ...s, valuation: false }));
    }
  }, []);

  const loadMovements = useCallback(async (from: string, to: string) => {
    setLoading((s) => ({ ...s, movements: true }));
    setError((s) => ({ ...s, movements: null }));
    try {
      const data = await reportsApi.inventoryMovements({
        from: new Date(from).toISOString(),
        to: new Date(to).toISOString(),
        take: 100,
      });
      setMovements(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, movements: getErrorMessage(e, 'تعذّر تحميل حركات المخزون.') }));
    } finally {
      setLoading((s) => ({ ...s, movements: false }));
    }
  }, []);

  const loadLowStock = useCallback(async () => {
    setLoading((s) => ({ ...s, 'low-stock': true }));
    setError((s) => ({ ...s, 'low-stock': null }));
    try {
      const data = await reportsApi.inventoryLowStock();
      setLowStock(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, 'low-stock': getErrorMessage(e, 'تعذّر تحميل تقرير تحت حد الطلب.') }));
    } finally {
      setLoading((s) => ({ ...s, 'low-stock': false }));
    }
  }, []);

  const loadAging = useCallback(async () => {
    setLoading((s) => ({ ...s, aging: true }));
    setError((s) => ({ ...s, aging: null }));
    try {
      const data = await reportsApi.inventoryAging();
      setAging(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, aging: getErrorMessage(e, 'تعذّر تحميل تقرير أعمار المخزون.') }));
    } finally {
      setLoading((s) => ({ ...s, aging: false }));
    }
  }, []);

  // Initial load
  useEffect(() => {
    loadValuation();
    loadMovements(fromDate, toDate);
    loadLowStock();
    loadAging();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Filter valuation by item filter
  const valuationRows = valuation?.items.filter((v) => {
    if (!itemFilter) return true;
    const q = itemFilter.toLowerCase();
    return v.itemSku.toLowerCase().includes(q) || v.itemName.toLowerCase().includes(q);
  }) || [];

  return (
    <div>
      <PageHeader
        title="📦 تقارير المخزون"
        description="تقييم المخزون، الحركات، تحت حد الطلب، وأعمار المخزون"
      />

      {/* Tabs */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-100 mb-4 p-1 inline-flex flex-wrap gap-1">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={
              'px-4 py-2 text-sm font-semibold rounded-lg transition-colors ' +
              (tab === t.key ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-50')
            }
          >
            <span className="ml-1">{t.icon}</span>
            {t.label}
          </button>
        ))}
      </div>

      {/* === Tab: تقييم المخزون === */}
      {tab === 'valuation' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <div className="flex-1 min-w-[200px]">
                <label className="block text-sm font-medium text-gray-700 mb-1">فلترة الصنف (SKU/اسم)</label>
                <Input
                  type="text"
                  value={itemFilter}
                  onChange={(e) => setItemFilter(e.target.value)}
                  placeholder="🔍 ابحث..."
                  containerClassName="w-full"
                />
              </div>
              <Button
                variant="ghost"
                onClick={loadValuation}
                iconLeft={<RefreshCcw className="h-4 w-4" />}
              >
                تحديث
              </Button>
            </div>
          </Card>

          {error['valuation'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['valuation']}
            </div>
          )}

          {valuation && (
            <Card className="mb-4">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-center">
                <div className="bg-blue-50 rounded-lg p-4">
                  <p className="text-xs text-gray-500">عدد السجلات</p>
                  <p className="font-mono font-bold text-blue-700 text-2xl">{valuation.count}</p>
                </div>
                <div className="bg-green-50 rounded-lg p-4">
                  <p className="text-xs text-gray-500">القيمة الإجمالية</p>
                  <p className="font-mono font-bold text-green-700 text-2xl">
                    {formatMoney(valuation.totalValue)}
                  </p>
                </div>
              </div>
            </Card>
          )}

          <Table
            columns={[
              {
                key: 'item',
                header: 'الصنف',
                render: (v) => (
                  <div>
                    <p className="font-mono text-xs text-gray-500">{v.itemSku}</p>
                    <p className="font-semibold text-gray-800">{v.itemName}</p>
                  </div>
                ),
              },
              { key: 'warehouse', header: 'المخزن', render: (v) => v.warehouseName },
              {
                key: 'qty',
                header: 'الكمية',
                align: 'end',
                render: (v) => <span className="font-mono">{formatNumber(v.quantityOnHand)}</span>,
              },
              {
                key: 'avgCost',
                header: 'متوسط التكلفة',
                align: 'end',
                render: (v) => <span className="font-mono">{formatNumber(v.averageCost)}</span>,
              },
              {
                key: 'value',
                header: 'القيمة',
                align: 'end',
                render: (v) => <span className="font-mono font-bold text-blue-700">{formatMoney(v.totalValue)}</span>,
              },
            ]}
            data={valuationRows}
            loading={loading.valuation}
            rowKey={(v) => `${v.itemId}-${v.warehouseId}`}
            emptyMessage="لا توجد أرصدة مخزون في هذا الـ tenant."
          />
        </div>
      )}

      {/* === Tab: حركات المخزون === */}
      {tab === 'movements' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">من تاريخ</label>
                <Input
                  type="date"
                  value={fromDate}
                  onChange={(e) => {
                    setFromDate(e.target.value);
                    loadMovements(e.target.value, toDate);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">إلى تاريخ</label>
                <Input
                  type="date"
                  value={toDate}
                  onChange={(e) => {
                    setToDate(e.target.value);
                    loadMovements(fromDate, e.target.value);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div className="text-xs text-gray-500">يعرض آخر 100 حركة</div>
            </div>
          </Card>

          {error['movements'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['movements']}
            </div>
          )}

          {movements && (
            <div className="mb-3 text-sm text-gray-600">
              عدد النتائج: <span className="font-bold">{movements.count}</span>
            </div>
          )}

          <Table
            columns={[
              { key: 'date', header: 'التاريخ', render: (m) => formatDateTime(m.movementDate) },
              { key: 'ref', header: 'المرجع', render: (m) => <span className="font-mono text-xs">{m.reference}</span> },
              {
                key: 'type',
                header: 'النوع',
                render: (m) => (
                  <Badge variant={MOVEMENT_TYPE_VARIANT[m.type] || 'neutral'}>
                    {STOCK_MOVEMENT_TYPE_LABELS[m.type] || m.type}
                  </Badge>
                ),
              },
              {
                key: 'qty',
                header: 'الكمية',
                align: 'end',
                render: (m) => <span className="font-mono">{formatNumber(m.quantity)}</span>,
              },
              {
                key: 'unitCost',
                header: 'تكلفة الوحدة',
                align: 'end',
                render: (m) => <span className="font-mono">{formatNumber(m.unitCost)}</span>,
              },
              { key: 'wh', header: 'المخزن', render: (m) => <span className="font-mono text-xs">{m.warehouseCode}</span> },
              { key: 'notes', header: 'ملاحظات', render: (m) => <span className="text-xs text-gray-500">{m.notes || '—'}</span> },
            ]}
            data={movements?.items || []}
            loading={loading.movements}
            rowKey={(m) => m.movementId}
            emptyMessage="لا توجد حركات في هذه الفترة."
          />
        </div>
      )}

      {/* === Tab: تحت حد الطلب === */}
      {tab === 'low-stock' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <Button onClick={loadLowStock} variant="ghost" iconLeft={<RefreshCcw className="h-4 w-4" />}>
                تحديث
              </Button>
              <div className="text-xs text-gray-500">
                أصناف وصلت لأقل من حد الطلب — اطلب فوراً
              </div>
            </div>
          </Card>

          {error['low-stock'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['low-stock']}
            </div>
          )}

          {lowStock && (
            <Card className="mb-4">
              <div className="flex items-center gap-3">
                <Badge variant="danger">{lowStock.count} صنف</Badge>
                <span className="text-sm text-gray-600">يحتاج إعادة طلب</span>
              </div>
            </Card>
          )}

          <Table
            columns={[
              {
                key: 'item',
                header: 'الصنف',
                render: (l) => (
                  <div>
                    <p className="font-mono text-xs text-gray-500">{l.itemSku}</p>
                    <p className="font-semibold text-gray-800">{l.itemName}</p>
                  </div>
                ),
              },
              { key: 'warehouse', header: 'المخزن', render: (l) => l.warehouseName },
              {
                key: 'qtyOnHand',
                header: 'الكمية',
                align: 'end',
                render: (l) => <span className="font-mono">{formatNumber(l.quantityOnHand)}</span>,
              },
              {
                key: 'reorderLevel',
                header: 'حد الطلب',
                align: 'end',
                render: (l) => <span className="font-mono">{formatNumber(l.reorderLevel)}</span>,
              },
              {
                key: 'shortfall',
                header: 'العجز',
                align: 'end',
                render: (l) => <span className="font-mono text-red-700 font-bold">{formatNumber(l.shortfall)}</span>,
              },
              {
                key: 'reorderQty',
                header: 'كمية الطلب',
                align: 'end',
                render: (l) => <span className="font-mono">{formatNumber(l.reorderQuantity)}</span>,
              },
              {
                key: 'status',
                header: 'الحالة',
                render: (l) => (
                  <Badge variant={LOW_STOCK_STATUS_VARIANT[l.status] || 'info'}>
                    {LOW_STOCK_STATUS_LABEL[l.status] || l.status}
                  </Badge>
                ),
              },
            ]}
            data={lowStock?.items || []}
            loading={loading['low-stock']}
            rowKey={(l) => `${l.itemId}-${l.warehouseId}`}
            emptyMessage="لا توجد أصناف تحت حد الطلب. ممتاز!"
          />
        </div>
      )}

      {/* === Tab: أعمار المخزون === */}
      {tab === 'aging' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <Button onClick={loadAging} variant="ghost" iconLeft={<RefreshCcw className="h-4 w-4" />}>
                تحديث
              </Button>
              <div className="text-xs text-gray-500">
                يعرض المخزون الراكد بحسب آخر حركة
              </div>
            </div>
          </Card>

          {error.aging && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error.aging}
            </div>
          )}

          {aging && (
            <Card className="mb-4">
              <div className="flex items-center gap-3">
                <Badge variant="info">{aging.count} صنف</Badge>
                <span className="text-sm text-gray-600">آخر حركة في النظام</span>
              </div>
            </Card>
          )}

          <Table
            columns={[
              {
                key: 'item',
                header: 'الصنف',
                render: (a) => (
                  <div>
                    <p className="font-mono text-xs text-gray-500">{a.sku}</p>
                    <p className="font-semibold text-gray-800">{a.name}</p>
                  </div>
                ),
              },
              {
                key: 'qtyOnHand',
                header: 'الكمية',
                align: 'end',
                render: (a) => <span className="font-mono">{formatNumber(a.quantityOnHand)}</span>,
              },
              {
                key: 'lastMove',
                header: 'آخر حركة',
                render: (a) => <span className="text-sm">{formatDate(a.lastMovementAt)}</span>,
              },
              {
                key: 'days',
                header: 'أيام في المخزن',
                align: 'center',
                render: (a) => <span className="font-mono">{a.daysInStock ?? '—'}</span>,
              },
              {
                key: 'bucket',
                header: 'الفئة',
                render: (a) => (
                  <Badge variant={AGING_BUCKET_VARIANT[a.ageBucket] || 'neutral'}>
                    {a.ageBucket || '—'}
                  </Badge>
                ),
              },
            ]}
            data={aging?.items || []}
            loading={loading.aging}
            rowKey={(a) => `${a.itemId}-${a.warehouseId}`}
            emptyMessage="لا توجد بيانات أعمار."
          />
        </div>
      )}
    </div>
  );
}
