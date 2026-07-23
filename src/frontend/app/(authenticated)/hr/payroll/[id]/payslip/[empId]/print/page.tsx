'use client';

// طباعة قسيمة الراتب (Payslip Print) — مع توقيعات
// v1.0.32: قالب رسمي

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { PrintLayout } from '@/components/print/PrintLayout';
import { hrApi, Company, PayrollItem, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function PrintPayslipPage() {
  const params = useParams<{ id: string; empId: string }>();
  const runId = params?.id;
  const empId = params?.empId;
  const [payslip, setPayslip] = useState<PayrollItem | null>(null);
  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!runId || !empId) return;
    Promise.all([
      hrApi.payroll.getPayrollRunItem(runId, empId).catch(() => null),
      fetch(`/api/companies`, { headers: { Authorization: `Bearer ${localStorage.getItem('accessToken')}` } })
        .then((r) => (r.ok ? r.json() : null))
        .catch(() => null),
    ])
      .then(([p, comps]) => {
        if (!p) throw new Error('قسيمة الراتب غير موجودة');
        setPayslip(p);
        const list = Array.isArray(comps) ? comps : [];
        const holding = list.find((c: Company) => c.isGroup) || list[0] || null;
        setCompany(holding);
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل التحميل.')))
      .finally(() => setLoading(false));
  }, [runId, empId]);

  if (loading) return <div className="p-8 text-center text-gray-500 no-print">جاري التحميل…</div>;
  if (error || !payslip) {
    return <div className="p-8 text-center text-red-600 no-print">{error || 'قسيمة الراتب غير موجودة.'}</div>;
  }

  const components = payslip.components || [];
  const earnings = components.filter((c) => c.componentType === 1);
  const deductions = components.filter((c) => c.componentType === 2);

  return (
    <PrintLayout documentTitle={`قسيمة راتب ${payslip.employeeName || ''}`}>
      {/* Header */}
      <div className="flex justify-between items-start border-b-2 border-gray-800 pb-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{company?.name || 'الشركة'}</h1>
          {company?.address && <p className="text-xs text-gray-500 mt-1">📍 {company.address}</p>}
          {company?.taxId && <p className="text-xs text-gray-500">🏛️ {company.taxId}</p>}
        </div>
        <div className="text-end">
          <h2 className="text-3xl font-bold text-purple-700 mb-1">قسيمة راتب</h2>
          <p className="text-sm text-gray-600">PAYSLIP</p>
        </div>
      </div>

      {/* Employee info */}
      <div className="grid grid-cols-2 gap-6 mb-6">
        <div className="bg-purple-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-purple-700 uppercase mb-2">الموظف</h3>
          <p className="font-bold text-lg text-gray-900">{payslip.employeeName || '—'}</p>
          {payslip.employeeNumber && <p className="text-xs text-gray-500 font-mono">الرقم الوظيفي: {payslip.employeeNumber}</p>}
        </div>
        <div className="bg-gray-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-2">دورة الراتب</h3>
          <div className="text-sm space-y-1">
            <div className="flex justify-between"><span className="text-gray-600">رقم الدورة:</span><span className="font-mono text-xs">{payslip.payrollRunId?.substring(0, 8)}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">أيام العمل:</span><span className="font-mono">{payslip.paymentDays}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">الحالة:</span><span className="font-semibold">{payslip.status}</span></div>
          </div>
        </div>
      </div>

      {/* Earnings + Deductions */}
      <div className="grid grid-cols-2 gap-6 mb-6">
        {/* Earnings */}
        <div>
          <h3 className="bg-green-100 text-green-800 px-3 py-2 font-bold text-sm rounded-t">المستحقات (+)</h3>
          <table className="w-full border-collapse">
            <tbody>
              {earnings.length === 0 ? (
                <tr><td className="py-3 text-center text-gray-400 text-sm border-b">لا توجد</td></tr>
              ) : (
                earnings.map((c, i) => (
                  <tr key={i} className="border-b">
                    <td className="py-2 px-2 text-sm">{c.name}</td>
                    <td className="py-2 px-2 text-sm font-mono text-end">{formatNumber(c.amount)}</td>
                  </tr>
                ))
              )}
              <tr className="bg-green-50 font-bold">
                <td className="py-2 px-2 text-sm">إجمالي المستحقات (Gross)</td>
                <td className="py-2 px-2 text-end text-sm font-mono">{formatNumber(payslip.grossSalary)}</td>
              </tr>
            </tbody>
          </table>
        </div>
        {/* Deductions */}
        <div>
          <h3 className="bg-red-100 text-red-800 px-3 py-2 font-bold text-sm rounded-t">الاستقطاعات (−)</h3>
          <table className="w-full border-collapse">
            <tbody>
              {deductions.length === 0 && payslip.taxAmount === 0 && payslip.socialInsuranceEmployee === 0 ? (
                <tr><td className="py-3 text-center text-gray-400 text-sm border-b">لا توجد</td></tr>
              ) : (
                <>
                  {deductions.map((c, i) => (
                    <tr key={i} className="border-b">
                      <td className="py-2 px-2 text-sm">{c.name}</td>
                      <td className="py-2 px-2 text-sm font-mono text-end">{formatNumber(c.amount)}</td>
                    </tr>
                  ))}
                  {payslip.taxAmount > 0 && (
                    <tr className="border-b">
                      <td className="py-2 px-2 text-sm">ضريبة الدخل</td>
                      <td className="py-2 px-2 text-sm font-mono text-end">{formatNumber(payslip.taxAmount)}</td>
                    </tr>
                  )}
                  {payslip.socialInsuranceEmployee > 0 && (
                    <tr className="border-b">
                      <td className="py-2 px-2 text-sm">التأمينات الاجتماعية (حصة الموظف)</td>
                      <td className="py-2 px-2 text-sm font-mono text-end">{formatNumber(payslip.socialInsuranceEmployee)}</td>
                    </tr>
                  )}
                </>
              )}
              <tr className="bg-red-50 font-bold">
                <td className="py-2 px-2 text-sm">إجمالي الخصومات</td>
                <td className="py-2 px-2 text-end text-sm font-mono">{formatNumber(Number(payslip.grossSalary) - Number(payslip.netSalary))}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      {/* Net Salary */}
      <div className="bg-purple-50 border-2 border-purple-300 rounded-lg p-6 text-center mb-6">
        <p className="text-sm text-gray-600 mb-1">صافي الراتب (Net Salary)</p>
        <p className="text-4xl font-bold text-purple-700 font-mono">{formatNumber(payslip.netSalary)} LYD</p>
        <p className="text-xs text-gray-500 mt-2">فقط {numberToArabicWords(Number(payslip.netSalary))} دينار ليبي لا غير</p>
      </div>

      {payslip.notes && (
        <div className="mb-6">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-1">ملاحظات</h3>
          <p className="text-sm text-gray-700 bg-gray-50 rounded p-3">{payslip.notes}</p>
        </div>
      )}

      {/* Signatures */}
      <div className="border-t-2 border-gray-800 pt-6 mt-12 grid grid-cols-3 gap-4 text-center text-sm text-gray-600">
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">المحاسب</div>
        </div>
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">مدير HR</div>
        </div>
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">توقيع الموظف</div>
        </div>
      </div>
    </PrintLayout>
  );
}

function numberToArabicWords(n: number): string {
  if (n === 0) return 'صفر';
  const ones = ['', 'واحد', 'اثنان', 'ثلاثة', 'أربعة', 'خمسة', 'ستة', 'سبعة', 'ثمانية', 'تسعة'];
  const tens = ['', 'عشرة', 'عشرون', 'ثلاثون', 'أربعون', 'خمسون', 'ستون', 'سبعون', 'ثمانون', 'تسعون'];
  const hundreds = ['', 'مائة', 'مئتان', 'ثلاثمائة', 'أربعمائة', 'خمسمائة', 'ستمائة', 'سبعمائة', 'ثمانمائة', 'تسعمائة'];
  const intPart = Math.floor(n);
  if (intPart < 10) return ones[intPart];
  if (intPart < 100) return tens[Math.floor(intPart / 10)] + (intPart % 10 ? ' و' + ones[intPart % 10] : '');
  if (intPart < 1000) return hundreds[Math.floor(intPart / 100)] + (intPart % 100 ? ' و' + tens[Math.floor((intPart % 100) / 10)] + (intPart % 10 ? ' و' + ones[intPart % 10] : '') : '');
  return `${Math.floor(intPart / 1000)} ألف و${numberToArabicWords(intPart % 1000)}`;
}
