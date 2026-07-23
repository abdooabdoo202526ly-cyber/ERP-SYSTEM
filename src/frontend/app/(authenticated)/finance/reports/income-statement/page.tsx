'use client';

// v1.0.33: تقرير الأرباح والخسائر (P&L / Income Statement)
// يعرض: Revenue - COGS = Gross Profit, Operating Expenses, Other Income/Expenses, Net Income

import { useEffect, useState } from 'react';
import { TrendingUp, TrendingDown, DollarSign, Calendar } from 'lucide-react';
import { Card, PageHeader, Input, Badge } from '@/components/ui';
import { reportsApi, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function IncomeStatementPage() {
  const today = new Date();
  const yearStart = new Date(today.getFullYear(), 0, 1).toISOString().slice(0, 10);
  const todayStr = today.toISOString().slice(0, 10);

  const [from, setFrom] = useState(yearStart);
  const [to, setTo] = useState(todayStr);
  const [report, setReport] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    load();
  }, []);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await reportsApi.incomeStatement(from, to);
      setReport(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل تقرير الأرباح والخسائر.'));
    } finally {
      setLoading(false);
    }
  };

  const isProfitable = (report?.netIncome ?? 0) >= 0;

  return (
    <div>
      <PageHeader
        title="📊 الأرباح والخسائر (P&L)"
        description="قائمة الدخل — الإيرادات والمصروفات وصافي الربح"
        actions={
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-gray-500" />
            <Input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
              className="w-40"
            />
            <span className="text-gray-500">-</span>
            <Input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
              className="w-40"
            />
            <button
              onClick={load}
              className="px-3 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700"
            >
              تحديث
            </button>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      {loading ? (
        <div className="text-center text-gray-500 py-12">جاري التحميل…</div>
      ) : !report ? (
        <div className="text-center text-gray-500 py-12">لا توجد بيانات.</div>
      ) : (
        <div className="space-y-4">
          {/* Top KPI cards */}
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <KpiCard
              title="الإيرادات"
              value={report.totalRevenue ?? report.revenue ?? 0}
              color="emerald"
              icon={<TrendingUp className="h-5 w-5" />}
            />
            <KpiCard
              title="تكلفة المبيعات"
              value={report.totalCogs ?? report.cogs ?? 0}
              color="amber"
              icon={<DollarSign className="h-5 w-5" />}
            />
            <KpiCard
              title="إجمالي الربح"
              value={report.grossProfit ?? 0}
              color="blue"
              icon={<DollarSign className="h-5 w-5" />}
              highlight
            />
            <KpiCard
              title="صافي الدخل"
              value={report.netIncome ?? 0}
              color={isProfitable ? 'green' : 'red'}
              icon={isProfitable ? <TrendingUp className="h-5 w-5" /> : <TrendingDown className="h-5 w-5" />}
              highlight
            />
          </div>

          {/* Detailed table */}
          <Card>
            <h2 className="text-lg font-bold text-gray-800 mb-4 border-b pb-2">قائمة الدخل التفصيلية</h2>
            <table className="w-full text-sm">
              <tbody>
                <SectionRow title="الإيرادات" color="emerald" />
                <LineRow label="إيرادات المبيعات" value={report.revenue ?? 0} />
                <LineRow label="إيرادات أخرى" value={report.otherIncome ?? 0} />
                <TotalRow label="إجمالي الإيرادات" value={report.totalRevenue ?? report.revenue ?? 0} color="emerald" />

                <tr><td colSpan={2} className="py-2"></td></tr>

                <SectionRow title="تكلفة المبيعات" color="amber" />
                <LineRow label="تكلفة البضاعة المباعة (COGS)" value={report.cogs ?? report.totalCogs ?? 0} />
                <TotalRow label="إجمالي التكلفة" value={report.totalCogs ?? report.cogs ?? 0} color="amber" />

                <tr><td colSpan={2} className="py-2"></td></tr>

                <TotalRow label="إجمالي الربح (Gross Profit)" value={report.grossProfit ?? 0} color="blue" highlight />

                <tr><td colSpan={2} className="py-2"></td></tr>

                <SectionRow title="المصروفات التشغيلية" color="red" />
                <LineRow label="مصروفات تشغيلية" value={report.operatingExpenses ?? 0} />
                <LineRow label="مصروفات أخرى" value={report.otherExpenses ?? 0} />
                <TotalRow label="إجمالي المصروفات" value={report.totalExpenses ?? report.operatingExpenses ?? 0} color="red" />

                <tr><td colSpan={2} className="py-3"></td></tr>

                <tr className="border-t-2 border-gray-800 bg-gray-50">
                  <td className="py-3 px-3 font-bold text-gray-900 text-base">صافي الدخل (Net Income)</td>
                  <td className={`py-3 px-3 text-end font-bold font-mono text-lg ${isProfitable ? 'text-green-700' : 'text-red-700'}`}>
                    {formatNumber(report.netIncome ?? 0)} LYD
                  </td>
                </tr>
              </tbody>
            </table>
          </Card>

          <div className="text-xs text-gray-500 text-center">
            من {from} إلى {to} — البيانات من دليل الحسابات المُرحّل
          </div>
        </div>
      )}
    </div>
  );
}

function KpiCard({ title, value, color, icon, highlight }: any) {
  const colors: any = {
    emerald: 'bg-emerald-50 border-emerald-200 text-emerald-800',
    amber: 'bg-amber-50 border-amber-200 text-amber-800',
    blue: 'bg-blue-50 border-blue-200 text-blue-800',
    red: 'bg-red-50 border-red-200 text-red-800',
    green: 'bg-green-50 border-green-300 text-green-900',
  };
  return (
    <div className={`p-4 rounded-lg border-2 ${colors[color]} ${highlight ? 'shadow-md' : ''}`}>
      <div className="flex items-center gap-2 mb-2">
        {icon}
        <span className="text-sm font-semibold">{title}</span>
      </div>
      <div className="text-2xl font-bold font-mono">{formatNumber(value)}</div>
      <div className="text-xs opacity-70">LYD</div>
    </div>
  );
}

function SectionRow({ title, color }: any) {
  const colors: any = {
    emerald: 'bg-emerald-100 text-emerald-800',
    amber: 'bg-amber-100 text-amber-800',
    red: 'bg-red-100 text-red-800',
    blue: 'bg-blue-100 text-blue-800',
  };
  return (
    <tr className={colors[color]}>
      <td colSpan={2} className="py-2 px-3 font-bold text-sm">{title}</td>
    </tr>
  );
}

function LineRow({ label, value }: any) {
  return (
    <tr>
      <td className="py-1.5 px-6 text-gray-700">{label}</td>
      <td className="py-1.5 px-3 text-end font-mono text-sm">{formatNumber(value)}</td>
    </tr>
  );
}

function TotalRow({ label, value, color, highlight }: any) {
  const colors: any = {
    emerald: 'bg-emerald-50 text-emerald-900',
    amber: 'bg-amber-50 text-amber-900',
    red: 'bg-red-50 text-red-900',
    blue: 'bg-blue-50 text-blue-900',
  };
  return (
    <tr className={`${colors[color]} ${highlight ? 'border-t border-gray-400' : ''}`}>
      <td className={`py-2 px-3 font-bold text-sm`}>{label}</td>
      <td className={`py-2 px-3 text-end font-bold font-mono`}>{formatNumber(value)}</td>
    </tr>
  );
}
