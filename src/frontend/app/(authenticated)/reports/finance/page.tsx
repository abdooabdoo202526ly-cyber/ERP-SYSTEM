'use client';

// صفحة تقارير المالية — ثلاثة تبويبات:
//   1) ميزان المراجعة (Trial Balance)
//   2) قائمة الدخل (Income Statement)
//   3) الميزانية العمومية (Balance Sheet)

import { useEffect, useState, useCallback } from 'react';
import { Card, PageHeader, Input, Table, Badge } from '@/components/ui';
import { reportsApi, TrialBalanceReport, IncomeStatement, BalanceSheet, ACCOUNT_TYPE_LABELS, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatMoney, formatNumber } from '@/lib/format';

type TabKey = 'trial-balance' | 'income-statement' | 'balance-sheet';

const TABS: { key: TabKey; label: string; icon: string }[] = [
  { key: 'trial-balance', label: 'ميزان المراجعة', icon: '⚖️' },
  { key: 'income-statement', label: 'قائمة الدخل', icon: '📈' },
  { key: 'balance-sheet', label: 'الميزانية العمومية', icon: '🏛️' },
];

// تاريخ افتراضي = اليوم
const today = (): string => new Date().toISOString().slice(0, 10);
const yearStart = (): string => {
  const d = new Date();
  return `${d.getFullYear()}-01-01`;
};

export default function FinanceReportsPage() {
  const [tab, setTab] = useState<TabKey>('trial-balance');

  // فلاتر مشتركة
  const [asOfDate, setAsOfDate] = useState<string>(today());
  const [fromDate, setFromDate] = useState<string>(yearStart());
  const [toDate, setToDate] = useState<string>(today());

  // بيانات كل تقرير
  const [trialBalance, setTrialBalance] = useState<TrialBalanceReport | null>(null);
  const [income, setIncome] = useState<IncomeStatement | null>(null);
  const [balance, setBalance] = useState<BalanceSheet | null>(null);

  // حالة التحميل والخطأ (مستقلة لكل تبويب)
  const [loading, setLoading] = useState<Record<TabKey, boolean>>({
    'trial-balance': false,
    'income-statement': false,
    'balance-sheet': false,
  });
  const [error, setError] = useState<Record<TabKey, string | null>>({
    'trial-balance': null,
    'income-statement': null,
    'balance-sheet': null,
  });

  const loadTrialBalance = useCallback(async (date: string) => {
    setLoading((s) => ({ ...s, 'trial-balance': true }));
    setError((s) => ({ ...s, 'trial-balance': null }));
    try {
      const data = await reportsApi.trialBalance(date);
      setTrialBalance(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, 'trial-balance': getErrorMessage(e, 'تعذّر تحميل ميزان المراجعة.') }));
    } finally {
      setLoading((s) => ({ ...s, 'trial-balance': false }));
    }
  }, []);

  const loadIncomeStatement = useCallback(async (from: string, to: string) => {
    setLoading((s) => ({ ...s, 'income-statement': true }));
    setError((s) => ({ ...s, 'income-statement': null }));
    try {
      const data = await reportsApi.incomeStatement(from, to);
      setIncome(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, 'income-statement': getErrorMessage(e, 'تعذّر تحميل قائمة الدخل.') }));
    } finally {
      setLoading((s) => ({ ...s, 'income-statement': false }));
    }
  }, []);

  const loadBalanceSheet = useCallback(async (asOf: string) => {
    setLoading((s) => ({ ...s, 'balance-sheet': true }));
    setError((s) => ({ ...s, 'balance-sheet': null }));
    try {
      const data = await reportsApi.balanceSheet(asOf);
      setBalance(data);
    } catch (e: unknown) {
      setError((s) => ({ ...s, 'balance-sheet': getErrorMessage(e, 'تعذّر تحميل الميزانية العمومية.') }));
    } finally {
      setLoading((s) => ({ ...s, 'balance-sheet': false }));
    }
  }, []);

  // تحميل أولي عند فتح الصفحة
  useEffect(() => {
    loadTrialBalance(asOfDate);
    loadIncomeStatement(fromDate, toDate);
    loadBalanceSheet(asOfDate);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div>
      <PageHeader
        title="💰 تقارير المالية"
        description="ميزان المراجعة، قائمة الدخل، والميزانية العمومية"
      />

      {/* Tabs */}
      <div className="bg-white rounded-xl shadow-sm border border-gray-100 mb-4 p-1 inline-flex flex-wrap gap-1">
        {TABS.map((t) => (
          <button
            key={t.key}
            onClick={() => setTab(t.key)}
            className={
              'px-4 py-2 text-sm font-semibold rounded-lg transition-colors ' +
              (tab === t.key
                ? 'bg-blue-600 text-white'
                : 'text-gray-600 hover:bg-gray-50')
            }
          >
            <span className="ml-1">{t.icon}</span>
            {t.label}
          </button>
        ))}
      </div>

      {/* === Tab: ميزان المراجعة === */}
      {tab === 'trial-balance' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">حتى تاريخ</label>
                <Input
                  type="date"
                  value={asOfDate}
                  onChange={(e) => {
                    setAsOfDate(e.target.value);
                    loadTrialBalance(e.target.value);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div className="text-xs text-gray-500">
                يعرض جميع الحسابات مع أرصدة مدين/دائن
              </div>
            </div>
          </Card>

          {error['trial-balance'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['trial-balance']}
            </div>
          )}

          {trialBalance && (
            <Card className="mb-4">
              <div className="flex flex-wrap items-center gap-3 mb-3">
                <h3 className="font-bold text-gray-800">الإجماليات</h3>
                <Badge variant={trialBalance.isBalanced ? 'success' : 'danger'}>
                  {trialBalance.isBalanced ? '✅ ميزان متوازن' : '⚠️ غير متوازن'}
                </Badge>
                <span className="text-xs text-gray-500">بتاريخ {formatDate(trialBalance.asOfDate)}</span>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3 text-center">
                <div className="bg-blue-50 rounded-lg p-3">
                  <p className="text-xs text-gray-500">إجمالي المدين</p>
                  <p className="font-mono font-bold text-blue-700">{formatMoney(trialBalance.totalDebit)}</p>
                </div>
                <div className="bg-blue-50 rounded-lg p-3">
                  <p className="text-xs text-gray-500">إجمالي الدائن</p>
                  <p className="font-mono font-bold text-blue-700">{formatMoney(trialBalance.totalCredit)}</p>
                </div>
                <div className="bg-gray-50 rounded-lg p-3">
                  <p className="text-xs text-gray-500">الفرق (Variance)</p>
                  <p className="font-mono font-bold text-gray-800">{formatMoney(trialBalance.variance)}</p>
                </div>
              </div>
            </Card>
          )}

          <Table
            columns={[
              {
                key: 'code',
                header: 'الكود',
                render: (r) => <span className="font-mono text-blue-600">{r.accountCode}</span>,
              },
              { key: 'name', header: 'اسم الحساب', render: (r) => r.accountName },
              {
                key: 'type',
                header: 'النوع',
                render: (r) => <Badge variant="info">{ACCOUNT_TYPE_LABELS[r.accountType] || r.accountType}</Badge>,
              },
              {
                key: 'debit',
                header: 'مدين',
                align: 'end',
                render: (r) => <span className="font-mono text-green-700">{r.debit ? formatNumber(r.debit) : '—'}</span>,
              },
              {
                key: 'credit',
                header: 'دائن',
                align: 'end',
                render: (r) => <span className="font-mono text-red-700">{r.credit ? formatNumber(r.credit) : '—'}</span>,
              },
            ]}
            data={trialBalance?.rows || []}
            loading={loading['trial-balance']}
            rowKey={(r) => r.accountId}
            emptyMessage="لا توجد حسابات أو حركات في هذه الفترة."
          />
        </div>
      )}

      {/* === Tab: قائمة الدخل === */}
      {tab === 'income-statement' && (
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
                    loadIncomeStatement(e.target.value, toDate);
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
                    loadIncomeStatement(fromDate, e.target.value);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div className="text-xs text-gray-500">
                صافي الدخل = الإيرادات − تكلفة المبيعات − مصاريف تشغيلية + إيرادات أخرى − مصاريف أخرى
              </div>
            </div>
          </Card>

          {error['income-statement'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['income-statement']}
            </div>
          )}

          {income && (
            <Card>
              <div className="mb-3">
                <h3 className="font-bold text-gray-800">قائمة الدخل</h3>
                <p className="text-xs text-gray-500">
                  من {formatDate(income.from)} إلى {formatDate(income.to)}
                </p>
              </div>
              <div className="space-y-2 text-sm">
                <IncomeLine label="الإيرادات (Revenue)" value={income.revenue} positive />
                <IncomeLine label="تكلفة المبيعات (COGS)" value={income.cogs} negative />
                <div className="border-t pt-2">
                  <IncomeLine label="إجمالي الربح (Gross Profit)" value={income.grossProfit} emphasize />
                </div>
                <IncomeLine label="المصاريف التشغيلية" value={income.operatingExpenses} negative />
                <IncomeLine label="إيرادات أخرى" value={income.otherIncome} positive />
                <IncomeLine label="مصاريف أخرى" value={income.otherExpenses} negative />
                <div className="border-t-2 pt-2 mt-2">
                  <IncomeLine
                    label="صافي الدخل (Net Income)"
                    value={income.netIncome}
                    emphasize
                    variant={income.netIncome >= 0 ? 'success' : 'danger'}
                  />
                </div>
              </div>
            </Card>
          )}

          {!income && !loading['income-statement'] && !error['income-statement'] && (
            <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
              لا توجد بيانات لعرضها. اختر الفترة الزمنية المناسبة.
            </div>
          )}
        </div>
      )}

      {/* === Tab: الميزانية العمومية === */}
      {tab === 'balance-sheet' && (
        <div>
          <Card className="mb-4">
            <div className="flex flex-wrap items-end gap-3">
              <div>
                <label className="block text-sm font-medium text-gray-700 mb-1">حتى تاريخ</label>
                <Input
                  type="date"
                  value={asOfDate}
                  onChange={(e) => {
                    setAsOfDate(e.target.value);
                    loadBalanceSheet(e.target.value);
                  }}
                  containerClassName="w-48"
                />
              </div>
              <div className="text-xs text-gray-500">
                الأصول = الخصوم + حقوق الملكية
              </div>
            </div>
          </Card>

          {error['balance-sheet'] && (
            <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
              {error['balance-sheet']}
            </div>
          )}

          {balance && (
            <Card>
              <div className="mb-3 flex flex-wrap items-center gap-3">
                <h3 className="font-bold text-gray-800">الميزانية العمومية</h3>
                <Badge variant={balance.isBalanced ? 'success' : 'danger'}>
                  {balance.isBalanced ? '✅ متوازنة' : '⚠️ غير متوازنة'}
                </Badge>
                <span className="text-xs text-gray-500">بتاريخ {formatDate(balance.asOfDate)}</span>
              </div>
              <div className="space-y-3">
                <BalanceLine label="إجمالي الأصول (Total Assets)" value={balance.totalAssets} color="blue" />
                <BalanceLine label="إجمالي الخصوم (Total Liabilities)" value={balance.totalLiabilities} color="red" />
                <BalanceLine label="حقوق الملكية (Total Equity)" value={balance.totalEquity} color="green" />
                <div className="border-t-2 pt-2">
                  <BalanceLine
                    label="الخصوم + حقوق الملكية"
                    value={balance.totalLiabilitiesAndEquity}
                    color="gray"
                    emphasize
                  />
                </div>
                <div className="bg-gray-50 rounded-lg p-3 text-center text-xs text-gray-600">
                  الفرق بين الأصول والخصوم+حقوق الملكية:&nbsp;
                  <span className="font-mono font-bold">{formatMoney(balance.variance)}</span>
                </div>
              </div>
            </Card>
          )}

          {!balance && !loading['balance-sheet'] && !error['balance-sheet'] && (
            <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
              لا توجد بيانات لعرضها. اختر التاريخ المناسب.
            </div>
          )}
        </div>
      )}
    </div>
  );
}

// ===== Internal subcomponents =====

interface IncomeLineProps {
  label: string;
  value: number;
  positive?: boolean;
  negative?: boolean;
  emphasize?: boolean;
  variant?: 'success' | 'danger';
}

function IncomeLine({ label, value, positive, negative, emphasize, variant }: IncomeLineProps) {
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

interface BalanceLineProps {
  label: string;
  value: number;
  color: 'blue' | 'red' | 'green' | 'gray';
  emphasize?: boolean;
}

function BalanceLine({ label, value, color, emphasize }: BalanceLineProps) {
  const colorMap: Record<BalanceLineProps['color'], string> = {
    blue: 'text-blue-700',
    red: 'text-red-700',
    green: 'text-green-700',
    gray: 'text-gray-800',
  };
  return (
    <div className={'flex items-center justify-between rounded-lg p-3 ' + (emphasize ? 'bg-gray-100' : 'bg-gray-50')}>
      <span className="text-gray-700">{label}</span>
      <span className={'font-mono ' + (emphasize ? 'font-bold text-base ' : '') + colorMap[color]}>
        {formatMoney(value)}
      </span>
    </div>
  );
}
