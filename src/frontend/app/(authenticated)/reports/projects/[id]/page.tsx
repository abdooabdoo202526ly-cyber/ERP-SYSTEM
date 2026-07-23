'use client';

// صفحة تفاصيل تقرير مشروع واحد:
//   - Tab 1: قائمة الأرباح والخسائر (PnL)  — GET /api/reports/projects/{id}/pnl?from&to
//   - Tab 2: الميزانية مقابل المصروف       — GET /api/reports/projects/{id}/budget-vs-actual
//
// الـ PnL يتطلب فترة (from/to). الـ Budget vs Actual يُجلب تلقائياً.

import { useEffect, useState, useCallback } from 'react';
import Link from 'next/link';
import { useParams } from 'next/navigation';
import { ArrowRight, RefreshCcw } from 'lucide-react';
import { Card, PageHeader, Input, Badge, Button } from '@/components/ui';
import {
  reportsApi,
  ProjectPnL,
  ProjectBudgetVsActual,
  getErrorMessage,
} from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatMoney, formatNumber } from '@/lib/format';

type TabKey = 'pnl' | 'budget';

const TABS: { key: TabKey; label: string; icon: string }[] = [
  { key: 'pnl', label: 'الأرباح والخسائر (PnL)', icon: '💹' },
  { key: 'budget', label: 'الميزانية مقابل المصروف', icon: '📊' },
];

const today = (): string => new Date().toISOString().slice(0, 10);
const yearStart = (): string => `${new Date().getFullYear()}-01-01`;

export default function ProjectReportDetailPage() {
  const params = useParams<{ id: string }>();
  const projectId = params.id;
  const [tab, setTab] = useState<TabKey>('pnl');

  // PnL period
  const [fromDate, setFromDate] = useState<string>(yearStart());
  const [toDate, setToDate] = useState<string>(today());

  // Data
  const [pnl, setPnl] = useState<ProjectPnL | null>(null);
  const [budget, setBudget] = useState<ProjectBudgetVsActual | null>(null);

  // Loading & error per tab
  const [loading, setLoading] = useState<Record<TabKey, boolean>>({ pnl: false, budget: false });
  const [error, setError] = useState<Record<TabKey, string | null>>({ pnl: null, budget: null });

  const loadPnL = useCallback(
    async (from: string, to: string) => {
      setLoading((s) => ({ ...s, pnl: true }));
      setError((s) => ({ ...s, pnl: null }));
      try {
        const data = await reportsApi.projectPnL(projectId, new Date(from).toISOString(), new Date(to).toISOString());
        setPnl(data);
      } catch (e: unknown) {
        setError((s) => ({ ...s, pnl: getErrorMessage(e, 'تعذّر تحميل تقرير PnL.') }));
      } finally {
        setLoading((s) => ({ ...s, pnl: false }));
      }
    },
    [projectId]
  );

  const loadBudget = useCallback(async () => {
    setLoading((s) => ({ ...s, budget: true }));
    setError((s) => ({ ...s, budget: null }));
    try {
      const data = await reportsApi.projectBudgetVsActual(projectId);
      setBudget(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, budget: getErrorMessage(e, 'تعذّر تحميل تقرير الميزانية.') }));
    } finally {
      setLoading((s) => ({ ...s, budget: false }));
    }
  }, [projectId]);

  useEffect(() => {
    loadPnL(fromDate, toDate);
    loadBudget();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [projectId]);

  // Header title from data
  const projectTitle = pnl?.projectName || budget?.projectCode || 'تقرير مشروع';
  const projectCode = pnl?.projectCode || budget?.projectCode;

  return (
    <div>
      <PageHeader
        title="📋 تقرير مشروع"
        description={
          projectCode ? (
            <span>
              <span className="font-mono">{projectCode}</span> — {projectTitle}
            </span>
          ) : (
            'PnL + Budget vs Actual'
          )
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'تقارير المشاريع', href: '/reports/projects' },
          { label: projectCode || 'تفاصيل' },
        ]}
        actions={
          <Link href="/reports/projects">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للقائمة
            </Button>
          </Link>
        }
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

      {/* === Tab: PnL === */}
      {tab === 'pnl' && (
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
                    loadPnL(e.target.value, toDate);
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
                    loadPnL(fromDate, e.target.value);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div className="text-xs text-gray-500">
                صافي الربح = الإيرادات − (مواد + عمالة + مقاولين) − مصاريف مخصصة
              </div>
            </div>
          </Card>

          {error.pnl && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error.pnl}
            </div>
          )}

          {pnl && (
            <Card>
              <div className="mb-3">
                <h3 className="font-bold text-gray-800">قائمة الأرباح والخسائر</h3>
                <p className="text-xs text-gray-500">
                  من {formatDate(pnl.from)} إلى {formatDate(pnl.to)}
                </p>
              </div>

              <div className="space-y-2 text-sm">
                <PnLLine label="الإيرادات (Revenue)" value={pnl.revenue} positive />
                <div className="text-xs text-gray-500 mt-3 mb-1">التكاليف المباشرة</div>
                <PnLLine label="تكاليف المواد" value={pnl.materialCost} negative />
                <PnLLine label="تكاليف العمالة" value={pnl.laborCost} negative />
                <PnLLine label="تكاليف المقاولين" value={pnl.subcontractorCost} negative />
                <div className="border-t pt-2">
                  <PnLLine label="إجمالي التكاليف المباشرة" value={pnl.directCosts} negative emphasize />
                </div>
                <PnLLine label="مصاريف مخصصة (Overhead)" value={pnl.allocatedOverhead} negative />
                <div className="border-t-2 pt-2 mt-2">
                  <PnLLine
                    label="صافي الربح (Net Profit)"
                    value={pnl.netProfit}
                    emphasize
                    variant={pnl.netProfit >= 0 ? 'success' : 'danger'}
                  />
                  <div className="flex items-center justify-between mt-1 text-xs">
                    <span className="text-gray-500">هامش الربح %</span>
                    <span
                      className={
                        'font-mono font-bold ' +
                        (pnl.marginPercent >= 0 ? 'text-green-700' : 'text-red-700')
                      }
                    >
                      {formatNumber(pnl.marginPercent, 2)}%
                    </span>
                  </div>
                </div>
              </div>
            </Card>
          )}

          {!pnl && !loading.pnl && !error.pnl && (
            <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
              لا توجد بيانات للمشروع في الفترة المختارة.
            </div>
          )}
        </div>
      )}

      {/* === Tab: Budget vs Actual === */}
      {tab === 'budget' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <Button onClick={loadBudget} variant="ghost" iconLeft={<RefreshCcw className="h-4 w-4" />}>
                تحديث
              </Button>
              <div className="text-xs text-gray-500">المتاح = الميزانية − المصروف − الملزم</div>
            </div>
          </Card>

          {error.budget && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error.budget}
            </div>
          )}

          {budget && (
            <div className="space-y-4">
              {/* KPIs */}
              <Card>
                <div className="grid grid-cols-2 md:grid-cols-4 gap-3 text-center">
                  <div className="bg-blue-50 rounded-lg p-4">
                    <p className="text-xs text-gray-500">الميزانية</p>
                    <p className="font-mono font-bold text-blue-700 text-lg">{formatMoney(budget.budgetAmount)}</p>
                  </div>
                  <div className="bg-orange-50 rounded-lg p-4">
                    <p className="text-xs text-gray-500">المنفق</p>
                    <p className="font-mono font-bold text-orange-700 text-lg">{formatMoney(budget.spentAmount)}</p>
                  </div>
                  <div className="bg-yellow-50 rounded-lg p-4">
                    <p className="text-xs text-gray-500">الملتزم به</p>
                    <p className="font-mono font-bold text-yellow-700 text-lg">{formatMoney(budget.committedAmount)}</p>
                  </div>
                  <div className="bg-green-50 rounded-lg p-4">
                    <p className="text-xs text-gray-500">المتبقي</p>
                    <p className="font-mono font-bold text-green-700 text-lg">{formatMoney(budget.availableAmount)}</p>
                  </div>
                </div>
              </Card>

              {/* نسبة الاستهلاك */}
              <Card title="نسبة الاستهلاك من الميزانية">
                <div className="mb-2 flex items-center justify-between text-sm">
                  <span className="text-gray-600">تم استخدام {formatNumber(budget.utilizationPercent, 2)}%</span>
                  <span className="font-mono font-bold">
                    {formatMoney(budget.spentAmount)} / {formatMoney(budget.budgetAmount)}
                  </span>
                </div>
                <div className="w-full h-4 bg-gray-200 rounded-full overflow-hidden">
                  <div
                    className={
                      'h-full transition-all ' +
                      (budget.utilizationPercent > 100
                        ? 'bg-red-500'
                        : budget.utilizationPercent > 80
                        ? 'bg-yellow-500'
                        : 'bg-green-500')
                    }
                    style={{ width: `${Math.min(100, budget.utilizationPercent)}%` }}
                  />
                </div>
              </Card>

              {/* Variance & last recalc */}
              <Card>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-sm">
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-500">الانحراف (Variance)</p>
                    <p
                      className={
                        'font-mono font-bold ' + (budget.variance >= 0 ? 'text-green-700' : 'text-red-700')
                      }
                    >
                      {formatMoney(budget.variance)}
                    </p>
                  </div>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-500">الانحراف %</p>
                    <p
                      className={
                        'font-mono font-bold ' + (budget.variancePercent >= 0 ? 'text-green-700' : 'text-red-700')
                      }
                    >
                      {formatNumber(budget.variancePercent, 2)}%
                    </p>
                  </div>
                  <div className="bg-gray-50 rounded-lg p-3">
                    <p className="text-xs text-gray-500">آخر إعادة حساب</p>
                    <p className="font-mono text-sm text-gray-800">
                      {budget.lastRecalculatedAt ? formatDate(budget.lastRecalculatedAt) : '—'}
                    </p>
                  </div>
                </div>
                {budget.utilizationPercent > 100 && (
                  <div className="mt-3">
                    <Badge variant="danger">⚠️ تجاوز الميزانية</Badge>
                  </div>
                )}
                {budget.utilizationPercent > 80 && budget.utilizationPercent <= 100 && (
                  <div className="mt-3">
                    <Badge variant="warning">⚠️ قارب على النفاد</Badge>
                  </div>
                )}
              </Card>
            </div>
          )}

          {!budget && !loading.budget && !error.budget && (
            <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
              لا توجد ميزانية مسجلة لهذا المشروع.
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ===== Internal subcomponents =====

interface PnLLineProps {
  label: string;
  value: number;
  positive?: boolean;
  negative?: boolean;
  emphasize?: boolean;
  variant?: 'success' | 'danger';
}

function PnLLine({ label, value, positive, negative, emphasize, variant }: PnLLineProps) {
  const color = emphasize
    ? variant === 'danger'
      ? 'text-red-700 font-bold text-base'
      : 'text-blue-700 font-bold text-base'
    : positive
    ? 'text-green-700'
    : negative
    ? 'text-red-700'
    : 'text-gray-800';
  return (
    <div className={'flex items-center justify-between ' + (emphasize ? 'py-1' : '')}>
      <span className="text-gray-700">{label}</span>
      <span className={'font-mono ' + color}>{formatMoney(value)}</span>
    </div>
  );
}
