'use client';

// v1.0.33: الميزانية العمومية (Balance Sheet)
// يعرض: الأصول، الالتزامات، حقوق الملكية، مع التحقق من التوازن

import { useEffect, useState } from 'react';
import { CheckCircle2, AlertCircle, Calendar, Building, Wallet, CreditCard, Briefcase } from 'lucide-react';
import { Card, PageHeader, Input, Badge } from '@/components/ui';
import { reportsApi, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function BalanceSheetPage() {
  const today = new Date().toISOString().slice(0, 10);
  const [asOf, setAsOf] = useState(today);
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
      const r = await reportsApi.balanceSheet(asOf);
      setReport(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الميزانية العمومية.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="📈 الميزانية العمومية (Balance Sheet)"
        description="الأصول والالتزامات وحقوق الملكية"
        actions={
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-gray-500" />
            <Input
              type="date"
              value={asOf}
              onChange={(e) => setAsOf(e.target.value)}
              className="w-44"
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
          {/* Balance check */}
          <Card className={report.isBalanced ? 'bg-green-50 border-green-300' : 'bg-red-50 border-red-300'}>
            <div className="flex items-center gap-3">
              {report.isBalanced ? (
                <CheckCircle2 className="h-8 w-8 text-green-600" />
              ) : (
                <AlertCircle className="h-8 w-8 text-red-600" />
              )}
              <div>
                <h3 className={`font-bold ${report.isBalanced ? 'text-green-800' : 'text-red-800'}`}>
                  {report.isBalanced ? 'الميزانية متوازنة ✓' : 'الميزانية غير متوازنة ✗'}
                </h3>
                {report.variance !== undefined && (
                  <p className="text-xs text-gray-600">الفرق: {formatNumber(report.variance)} LYD</p>
                )}
              </div>
            </div>
          </Card>

          {/* KPIs */}
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <KpiCard
              title="إجمالي الأصول"
              value={report.totalAssets ?? 0}
              color="blue"
              icon={<Building className="h-5 w-5" />}
            />
            <KpiCard
              title="إجمالي الالتزامات"
              value={report.totalLiabilities ?? 0}
              color="amber"
              icon={<CreditCard className="h-5 w-5" />}
            />
            <KpiCard
              title="حقوق الملكية"
              value={report.totalEquity ?? 0}
              color="emerald"
              icon={<Wallet className="h-5 w-5" />}
            />
          </div>

          {/* Detailed breakdown */}
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <BalanceCard
              title="الأصول"
              color="blue"
              icon={<Building className="h-5 w-5" />}
              total={report.totalAssets ?? 0}
              items={report.assets || []}
            />
            <BalanceCard
              title="الالتزامات"
              color="amber"
              icon={<CreditCard className="h-5 w-5" />}
              total={report.totalLiabilities ?? 0}
              items={report.liabilities || []}
            />
            <BalanceCard
              title="حقوق الملكية"
              color="emerald"
              icon={<Briefcase className="h-5 w-5" />}
              total={report.totalEquity ?? 0}
              items={report.equity || []}
            />
            <Card>
              <h3 className="text-lg font-bold mb-3">معادلة الميزانية</h3>
              <div className="text-sm space-y-2 font-mono">
                <div className="bg-blue-50 p-2 rounded">
                  <span className="text-gray-600">إجمالي الأصول</span>
                  <span className="float-end font-bold text-blue-700">{formatNumber(report.totalAssets ?? 0)}</span>
                </div>
                <div className="text-center text-gray-400">=</div>
                <div className="bg-amber-50 p-2 rounded">
                  <span className="text-gray-600">الالتزامات</span>
                  <span className="float-end font-bold text-amber-700">{formatNumber(report.totalLiabilities ?? 0)}</span>
                </div>
                <div className="text-center text-gray-400">+</div>
                <div className="bg-emerald-50 p-2 rounded">
                  <span className="text-gray-600">حقوق الملكية</span>
                  <span className="float-end font-bold text-emerald-700">{formatNumber(report.totalEquity ?? 0)}</span>
                </div>
                <div className="border-t-2 border-gray-300 pt-2 mt-2">
                  <span className="text-gray-700 font-semibold">المجموع</span>
                  <span className="float-end font-bold text-lg">{formatNumber(report.totalLiabilitiesAndEquity ?? 0)}</span>
                </div>
              </div>
            </Card>
          </div>

          <div className="text-xs text-gray-500 text-center">
            كما في {asOf} — يُحسب من دليل الحسابات
          </div>
        </div>
      )}
    </div>
  );
}

function KpiCard({ title, value, color, icon }: any) {
  const colors: any = {
    blue: 'bg-blue-50 border-blue-300 text-blue-900',
    amber: 'bg-amber-50 border-amber-300 text-amber-900',
    emerald: 'bg-emerald-50 border-emerald-300 text-emerald-900',
  };
  return (
    <div className={`p-4 rounded-lg border-2 ${colors[color]}`}>
      <div className="flex items-center gap-2 mb-2">
        {icon}
        <span className="text-sm font-semibold">{title}</span>
      </div>
      <div className="text-2xl font-bold font-mono">{formatNumber(value)}</div>
      <div className="text-xs opacity-70">LYD</div>
    </div>
  );
}

function BalanceCard({ title, color, icon, total, items }: any) {
  const colors: any = {
    blue: { bg: 'bg-blue-50', text: 'text-blue-800', header: 'bg-blue-100' },
    amber: { bg: 'bg-amber-50', text: 'text-amber-800', header: 'bg-amber-100' },
    emerald: { bg: 'bg-emerald-50', text: 'text-emerald-800', header: 'bg-emerald-100' },
  };
  const c = colors[color];
  return (
    <Card>
      <div className={`flex items-center gap-2 mb-3 p-2 rounded ${c.header}`}>
        {icon}
        <h3 className={`font-bold ${c.text}`}>{title}</h3>
        <span className="ms-auto font-bold font-mono text-sm">{formatNumber(total)}</span>
      </div>
      {items.length === 0 ? (
        <p className="text-xs text-gray-500 text-center py-4">لا توجد حسابات في هذه الفئة</p>
      ) : (
        <table className="w-full text-sm">
          <tbody>
            {items.map((it: any, i: number) => (
              <tr key={i} className="border-b border-gray-100">
                <td className="py-1.5">
                  <div className="font-mono text-xs text-gray-500">{it.accountCode}</div>
                  <div className="text-gray-700">{it.accountName}</div>
                </td>
                <td className="py-1.5 text-end font-mono">{formatNumber(it.amount)}</td>
              </tr>
            ))}
            <tr className={`${c.bg} font-bold`}>
              <td className="py-2 px-2">الإجمالي</td>
              <td className="py-2 px-2 text-end font-mono">{formatNumber(total)}</td>
            </tr>
          </tbody>
        </table>
      )}
    </Card>
  );
}
