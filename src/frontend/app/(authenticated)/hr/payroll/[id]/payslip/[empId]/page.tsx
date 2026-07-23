'use client';

// صفحة عرض قسيمة راتب موظف واحد (Payslip) — earnings / deductions / tax / net + زر طباعة

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, Printer, TrendingUp, TrendingDown } from 'lucide-react';
import { Button, Card, PageHeader } from '@/components/ui';
import {
  hrApi,
  Payslip,
  PayrollRun,
  COMPONENT_TYPES,
  COMPONENT_TYPE_LABELS,
  getErrorMessage,
} from '@/lib/api';

interface PageProps {
  params: { id: string; empId: string };
}

export default function PayslipPage({ params }: PageProps) {
  const { id: runId, empId } = params;
  const [run, setRun] = useState<PayrollRun | null>(null);
  const [payslip, setPayslip] = useState<Payslip | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [runId, empId]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [runData, payslipData] = await Promise.all([
        hrApi.payroll.getPayrollRun(runId),
        hrApi.payroll.getPayslip(runId, empId),
      ]);
      setRun(runData);
      setPayslip(payslipData);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل القسيمة.'));
    } finally {
      setLoading(false);
    }
  };

  const handlePrint = () => {
    if (typeof window !== 'undefined') {
      // v1.0.32: open dedicated print page
      window.open(`/hr/payroll/${runId}/payslip/${empId}/print`, '_blank');
    }
  };

  const formatDate = (s?: string) => (s ? new Date(s).toLocaleDateString('en-GB') : '—');  // DEC-077: BUG-008 — use en-GB (dd/MM/yyyy)
  const formatMoney = (n: number) => n?.toLocaleString(undefined, { minimumFractionDigits: 2 }) || '0.00';

  if (loading) {
    return (
      <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
        <p className="mt-3 text-sm">جاري التحميل...</p>
      </div>
    );
  }

  if (!payslip || !run) {
    return (
      <div>
        <PageHeader
          title="القسيمة غير موجودة"
          breadcrumb={[
            { label: 'Payroll', href: '/hr/payroll' },
            { label: 'الدورة', href: `/hr/payroll/${runId}` },
            { label: 'غير موجودة' },
          ]}
          actions={
            <Link href={`/hr/payroll/${runId}`}>
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع للدورة
              </Button>
            </Link>
          }
        />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
          {error || 'لم يتم العثور على القسيمة.'}
        </div>
      </div>
    );
  }

  // تصنيف الـ components إلى earnings / deductions
  const earnings = (payslip.components || []).filter(
    (c) => COMPONENT_TYPES[c.componentType] === 'earning'
  );
  const deductions = (payslip.components || []).filter(
    (c) => COMPONENT_TYPES[c.componentType] === 'deduction'
  );
  const totalEarnings = earnings.reduce((s, c) => s + c.amount, 0);
  const totalDeductions = deductions.reduce((s, c) => s + c.amount, 0);

  return (
    <div>
      <PageHeader
        title="📄 قسيمة راتب (Payslip)"
        description={`${payslip.employeeName || payslip.employeeId} — ${formatDate(run.periodStart)} → ${formatDate(run.periodEnd)}`}
        breadcrumb={[
          { label: 'Payroll', href: '/hr/payroll' },
          { label: `الدورة ${formatDate(run.periodStart)}`, href: `/hr/payroll/${runId}` },
          { label: payslip.employeeName || 'Payslip' },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" onClick={handlePrint} iconLeft={<Printer className="h-4 w-4" />}>
              طباعة
            </Button>
            <Link href={`/hr/payroll/${runId}`}>
              <Button variant="ghost" size="sm" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {/* ملخص الموظف + الفترة */}
      <Card className="mb-6">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4 text-sm">
          <div>
            <p className="text-xs text-gray-500">الموظف</p>
            <p className="font-bold text-gray-800">{payslip.employeeName || '—'}</p>
            <p className="text-xs text-gray-500 font-mono mt-0.5">
              {payslip.employeeNumber || payslip.employeeId.slice(0, 8)}
            </p>
          </div>
          <div>
            <p className="text-xs text-gray-500">الفترة</p>
            <p className="font-mono text-gray-800">
              {formatDate(run.periodStart)} → {formatDate(run.periodEnd)}
            </p>
          </div>
          <div>
            <p className="text-xs text-gray-500">أيام العمل</p>
            <p className="font-bold text-gray-800">{payslip.paymentDays} يوم</p>
          </div>
          <div>
            <p className="text-xs text-gray-500">الراتب الأساسي</p>
            <p className="font-mono font-bold text-gray-800">{formatMoney(payslip.baseSalary)} LYD</p>
          </div>
        </div>
      </Card>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4 mb-6">
        {/* Earnings */}
        <Card title={<span className="flex items-center gap-2"><TrendingUp className="h-4 w-4 text-green-600" /> المستحقات (Earnings)</span>} accent="green">
          {earnings.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">لا توجد مستحقات مفصّلة</p>
          ) : (
            <table className="w-full">
              <tbody>
                {earnings.map((c) => (
                  <tr key={c.id} className="border-b border-gray-100 last:border-0">
                    <td className="py-2 text-sm text-gray-700">{c.name}</td>
                    <td className="py-2 text-end font-mono text-sm text-green-700">
                      {formatMoney(c.amount)}
                    </td>
                  </tr>
                ))}
                <tr className="bg-green-50">
                  <td className="py-2 px-2 text-sm font-bold text-gray-800 rounded">إجمالي المستحقات</td>
                  <td className="py-2 px-2 text-end font-mono font-bold text-green-700 rounded">
                    {formatMoney(totalEarnings)}
                  </td>
                </tr>
              </tbody>
            </table>
          )}
        </Card>

        {/* Deductions */}
        <Card title={<span className="flex items-center gap-2"><TrendingDown className="h-4 w-4 text-red-600" /> المستقطعات (Deductions)</span>} accent="red">
          {deductions.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">لا توجد مستقطعات مفصّلة</p>
          ) : (
            <table className="w-full">
              <tbody>
                {deductions.map((c) => (
                  <tr key={c.id} className="border-b border-gray-100 last:border-0">
                    <td className="py-2 text-sm text-gray-700">{c.name}</td>
                    <td className="py-2 text-end font-mono text-sm text-red-600">
                      −{formatMoney(c.amount)}
                    </td>
                  </tr>
                ))}
                <tr className="bg-red-50">
                  <td className="py-2 px-2 text-sm font-bold text-gray-800 rounded">إجمالي المستقطعات</td>
                  <td className="py-2 px-2 text-end font-mono font-bold text-red-700 rounded">
                    −{formatMoney(totalDeductions)}
                  </td>
                </tr>
              </tbody>
            </table>
          )}
        </Card>
      </div>

      {/* Summary breakdown */}
      <Card title="ملخص الحساب" accent="blue">
        <div className="space-y-3">
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-600">إجمالي المستحقات (Gross)</span>
            <span className="font-mono font-bold text-gray-800">{formatMoney(payslip.grossSalary)} LYD</span>
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-600">ضريبة الدخل (GDT)</span>
            <span className="font-mono text-red-600">−{formatMoney(payslip.taxAmount)} LYD</span>
          </div>
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-600">التأمينات الاجتماعية (حصة الموظف)</span>
            <span className="font-mono text-red-600">−{formatMoney(payslip.socialInsuranceEmployee)} LYD</span>
          </div>
          <div className="flex items-center justify-between text-sm border-t border-gray-100 pt-3">
            <span className="text-gray-600">إجمالي الخصومات</span>
            <span className="font-mono text-red-600">−{formatMoney(payslip.taxAmount + payslip.socialInsuranceEmployee)} LYD</span>
          </div>
          <div className="flex items-center justify-between bg-green-50 -mx-5 -mb-5 px-5 py-3 rounded-b-xl border-t-2 border-green-200 mt-3">
            <span className="font-bold text-green-800">صافي الراتب (Net Salary)</span>
            <span className="font-mono text-2xl font-bold text-green-700">
              {formatMoney(payslip.netSalary)} LYD
            </span>
          </div>
        </div>
      </Card>

      {/* Print-only footer */}
      <div className="hidden print:block mt-8 pt-4 border-t border-gray-200 text-xs text-gray-500 text-center">
        <p>تم إصدار هذه القسيمة بتاريخ {formatDate(new Date().toISOString())}</p>
        <p className="font-mono mt-1">Payroll Run ID: {runId}</p>
      </div>
    </div>
  );
}