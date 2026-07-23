'use client';

// v1.0.33: ميزان المراجعة (Trial Balance)
// كل الحسابات مع أرصدة مدين/دائن + التحقق من التوازن

import { useEffect, useState } from 'react';
import { CheckCircle2, AlertCircle, Calendar } from 'lucide-react';
import { Card, PageHeader, Input, Badge, Table } from '@/components/ui';
import { reportsApi, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function TrialBalancePage() {
  const today = new Date().toISOString().slice(0, 10);
  const [asOfDate, setAsOfDate] = useState(today);
  const [report, setReport] = useState<any>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => { load(); }, []);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const r = await reportsApi.trialBalance(asOfDate);
      setReport(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل ميزان المراجعة.'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="📉 ميزان المراجعة (Trial Balance)"
        description="قيد لكل حساب بالأرصدة المدينة والدائنة"
        actions={
          <div className="flex items-center gap-2">
            <Calendar className="h-4 w-4 text-gray-500" />
            <Input type="date" value={asOfDate} onChange={(e) => setAsOfDate(e.target.value)} className="w-44" />
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
          <Card className={report.isBalanced ? 'bg-green-50 border-green-300' : 'bg-red-50 border-red-300'}>
            <div className="flex items-center gap-3">
              {report.isBalanced ? (
                <CheckCircle2 className="h-8 w-8 text-green-600" />
              ) : (
                <AlertCircle className="h-8 w-8 text-red-600" />
              )}
              <div>
                <h3 className={`font-bold ${report.isBalanced ? 'text-green-800' : 'text-red-800'}`}>
                  {report.isBalanced ? 'الميزان متوازن ✓' : 'الميزان غير متوازن ✗'}
                </h3>
                {report.variance !== undefined && (
                  <p className="text-xs text-gray-600">الفرق: {formatNumber(report.variance)} LYD</p>
                )}
              </div>
            </div>
          </Card>

          <Table
            columns={[
              {
                key: 'code',
                header: 'الكود',
                render: (r: any) => <span className="font-mono text-sm">{r.accountCode}</span>,
              },
              {
                key: 'name',
                header: 'اسم الحساب',
                render: (r: any) => <span className="font-semibold">{r.accountName}</span>,
              },
              {
                key: 'debit',
                header: 'مدين',
                align: 'end',
                render: (r: any) => r.debitBalance > 0 ? <span className="font-mono">{formatNumber(r.debitBalance)}</span> : <span className="text-gray-400">—</span>,
              },
              {
                key: 'credit',
                header: 'دائن',
                align: 'end',
                render: (r: any) => r.creditBalance > 0 ? <span className="font-mono">{formatNumber(r.creditBalance)}</span> : <span className="text-gray-400">—</span>,
              },
            ]}
            data={report.rows || []}
            loading={false}
            rowKey={(r: any) => r.accountId || r.id}
            emptyMessage="لا توجد حسابات."
          />

          {/* Totals */}
          <Card className="bg-gray-50">
            <div className="grid grid-cols-2 gap-4 text-center">
              <div>
                <div className="text-xs text-gray-600">إجمالي المدين</div>
                <div className="text-2xl font-bold font-mono text-blue-700">{formatNumber(report.totalDebit ?? 0)}</div>
              </div>
              <div>
                <div className="text-xs text-gray-600">إجمالي الدائن</div>
                <div className="text-2xl font-bold font-mono text-purple-700">{formatNumber(report.totalCredit ?? 0)}</div>
              </div>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
