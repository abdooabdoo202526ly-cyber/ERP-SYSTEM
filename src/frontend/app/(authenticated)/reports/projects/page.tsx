'use client';

// صفحة تقارير المشاريع — ملخص كل المشاريع (Projects Summary)
// GET /api/reports/projects/summary
// كل صف من الجدول قابل للنقر → ينقل لـ /reports/projects/[id] لتفاصيل PnL و Budget vs Actual.

import { useEffect, useState, useCallback } from 'react';
import { Card, PageHeader, Table, Badge, Button } from '@/components/ui';
import {
  reportsApi,
  ProjectsSummaryResponse,
  ProjectSummary,
  PROJECT_STATUS_LABELS,
  getErrorMessage,
} from '@/lib/api';
import { formatDateTime } from '@/lib/utils';
import { formatMoney, formatNumber } from '@/lib/format';
import { RefreshCcw } from 'lucide-react';

const PROJECT_STATUS_VARIANT: Record<string, 'success' | 'warning' | 'info' | 'danger' | 'neutral'> = {
  Active: 'success',
  Planning: 'info',
  OnHold: 'warning',
  Completed: 'neutral',
  Cancelled: 'danger',
};

export default function ProjectsReportsPage() {
  const [data, setData] = useState<ProjectsSummaryResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await reportsApi.projectsSummary();
      setData(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل ملخص المشاريع.'));
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const totalBudget = data?.items.reduce((sum, p) => sum + (p.budget || 0), 0) || 0;
  const totalSpent = data?.items.reduce((sum, p) => sum + (p.spent || 0), 0) || 0;

  return (
    <div>
      <PageHeader
        title="📊 تقارير المشاريع"
        description="ملخص كل المشاريع: الميزانية، المصروف، هامش الربح"
        actions={
          <Button onClick={load} variant="ghost" iconLeft={<RefreshCcw className="h-4 w-4" />}>
            تحديث
          </Button>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {data && (
        <Card className="mb-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-center">
            <div className="bg-blue-50 rounded-lg p-4">
              <p className="text-xs text-gray-500">عدد المشاريع</p>
              <p className="font-mono font-bold text-blue-700 text-2xl">{data.count}</p>
            </div>
            <div className="bg-purple-50 rounded-lg p-4">
              <p className="text-xs text-gray-500">إجمالي الميزانية</p>
              <p className="font-mono font-bold text-purple-700 text-2xl">{formatMoney(totalBudget)}</p>
            </div>
            <div className="bg-orange-50 rounded-lg p-4">
              <p className="text-xs text-gray-500">إجمالي المصروف</p>
              <p className="font-mono font-bold text-orange-700 text-2xl">{formatMoney(totalSpent)}</p>
            </div>
          </div>
        </Card>
      )}

      <Table<ProjectSummary>
        columns={[
          {
            key: 'code',
            header: 'الكود',
            render: (p) => <span className="font-mono text-blue-600">{p.code}</span>,
          },
          { key: 'name', header: 'الاسم', render: (p) => <span className="font-semibold text-gray-800">{p.name}</span> },
          {
            key: 'status',
            header: 'الحالة',
            render: (p) => (
              <Badge variant={PROJECT_STATUS_VARIANT[p.status] || 'neutral'}>
                {PROJECT_STATUS_LABELS[p.status] || p.status}
              </Badge>
            ),
          },
          {
            key: 'budget',
            header: 'الميزانية',
            align: 'end',
            render: (p) => <span className="font-mono">{formatMoney(p.budget)}</span>,
          },
          {
            key: 'spent',
            header: 'المصروف',
            align: 'end',
            render: (p) => <span className="font-mono text-orange-700">{formatMoney(p.spent)}</span>,
          },
          {
            key: 'margin',
            header: 'هامش %',
            align: 'end',
            render: (p) => {
              const m = p.marginPercent;
              const color = m >= 0 ? 'text-green-700' : 'text-red-700';
              return <span className={'font-mono font-bold ' + color}>{formatNumber(m, 2)}%</span>;
            },
          },
          {
            key: 'lastActivity',
            header: 'آخر نشاط',
            render: (p) => <span className="text-xs text-gray-500">{formatDateTime(p.lastActivity)}</span>,
          },
        ]}
        data={data?.items || []}
        loading={loading}
        rowKey={(p) => p.id}
        rowHref={(p) => `/reports/projects/${p.id}`}
        emptyMessage="لا توجد مشاريع في هذا الـ tenant."
      />
    </div>
  );
}
