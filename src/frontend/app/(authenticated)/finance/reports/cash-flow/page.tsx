'use client';

// v1.0.33: التدفقات النقدية (Cash Flow Statement)

import { useEffect, useState } from 'react';
import { TrendingUp, TrendingDown, Wallet, Calendar } from 'lucide-react';
import { Card, PageHeader, Input, Badge } from '@/components/ui';
import { reportsApi, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function CashFlowPage() {
  const today = new Date();
  const monthStart = new Date(today.getFullYear(), today.getMonth(), 1).toISOString().slice(0, 10);
  const todayStr = today.toISOString().slice(0, 10);

  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(todayStr);
  const [report, setReport] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { load(); }, []);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await reportsApi.cashFlow(from, to);
      setReport(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل التدفقات النقدية.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="💰 التدفقات النقدية (Cash Flow)"
        description="حركة النقد الداخل والخارج خلال الفترة"
        actions={
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-gray-500" />
            <Input type="date" value={from} onChange={(e) => setFrom(e.target.value)} className="w-40" />
            <span>-</span>
            <Input type="date" value={to} onChange={(e) => setTo(e.target.value)} className="w-40" />
            <button onClick={load} className="px-3 py-2 bg-blue-600 text-white rounded text-sm hover:bg-blue-700">
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
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <KpiCard
              title="التدفق التشغيلي"
              value={report.operatingCashFlow ?? 0}
              color={(report.operatingCashFlow ?? 0) >= 0 ? 'emerald' : 'red'}
              icon={(report.operatingCashFlow ?? 0) >= 0 ? <TrendingUp className="h-5 w-5" /> : <TrendingDown className="h-5 w-5" />}
            />
            <KpiCard
              title="التدفق الاستثماري"
              value={report.investingCashFlow ?? 0}
              color={(report.investingCashFlow ?? 0) >= 0 ? 'emerald' : 'red'}
              icon={(report.investingCashFlow ?? 0) >= 0 ? <TrendingUp className="h-5 w-5" /> : <TrendingDown className="h-5 w-5" />}
            />
            <KpiCard
              title="صافي التدفق"
              value={report.netCashFlow ?? (report.operatingCashFlow ?? 0) + (report.investingCashFlow ?? 0) + (report.financingCashFlow ?? 0)}
              color={(report.netCashFlow ?? 0) >= 0 ? 'emerald' : 'red'}
              icon={<Wallet className="h-5 w-5" />}
              highlight
            />
          </div>

          <Card>
            <h2 className="text-lg font-bold text-gray-800 mb-4 border-b pb-2">تفاصيل التدفقات النقدية</h2>
            <table className="w-full text-sm">
              <tbody>
                <SectionRow title="التدفقات التشغيلية (Operating)" color="emerald" />
                <LineRow label="تحصيلات من العملاء" value={report.collectionsFromCustomers ?? 0} />
                <LineRow label="مدفوعات للموردين" value={report.paymentsToSuppliers ?? 0} />
                <LineRow label="مدفوعات رواتب" value={report.salaryPayments ?? 0} />
                <TotalRow label="صافي التشغيل" value={report.operatingCashFlow ?? 0} color="emerald" />

                <tr><td colSpan={2} className="py-2"></td></tr>

                <SectionRow title="التدفقات الاستثمارية (Investing)" color="amber" />
                <LineRow label="شراء أصول ثابتة" value={report.assetPurchases ?? 0} />
                <LineRow label="بيع أصول" value={report.assetSales ?? 0} />
                <TotalRow label="صافي الاستثمار" value={report.investingCashFlow ?? 0} color="amber" />

                <tr><td colSpan={2} className="py-2"></td></tr>

                <SectionRow title="التدفقات التمويلية (Financing)" color="blue" />
                <LineRow label="قروض" value={report.loansReceived ?? 0} />
                <LineRow label="سداد قروض" value={report.loanRepayments ?? 0} />
                <TotalRow label="صافي التمويل" value={report.financingCashFlow ?? 0} color="blue" />

                <tr><td colSpan={2} className="py-3"></td></tr>

                <tr className="border-t-2 border-gray-800 bg-gray-50">
                  <td className="py-3 px-3 font-bold text-base">صافي التدفق النقدي</td>
                  <td className="py-3 px-3 text-end font-bold font-mono text-lg text-gray-900">
                    {formatNumber(report.netCashFlow ?? 0)} LYD
                  </td>
                </tr>
              </tbody>
            </table>
          </Card>

          <div className="text-xs text-gray-500 text-center">
            من {from} إلى {to}
          </div>
        </div>
      )}
    </div>
  );
}

function KpiCard({ title, value, color, icon, highlight }: any) {
  const colors: any = {
    emerald: 'bg-emerald-50 border-emerald-300 text-emerald-900',
    red: 'bg-red-50 border-red-300 text-red-900',
    amber: 'bg-amber-50 border-amber-300 text-amber-900',
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

function TotalRow({ label, value, color }: any) {
  const colors: any = {
    emerald: 'bg-emerald-50 text-emerald-900',
    amber: 'bg-amber-50 text-amber-900',
    blue: 'bg-blue-50 text-blue-900',
  };
  return (
    <tr className={`${colors[color]} font-bold`}>
      <td className="py-2 px-3 text-sm">{label}</td>
      <td className="py-2 px-3 text-end font-mono">{formatNumber(value)}</td>
    </tr>
  );
}
