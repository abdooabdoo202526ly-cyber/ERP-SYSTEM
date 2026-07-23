'use client';

// طباعة سند القبض (Receipt Print)
// v1.0.32: قالب رسمي + تفاصيل التخصيص على الفواتير

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import { PrintLayout } from '@/components/print/PrintLayout';
import { arApi, Company, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function PrintReceiptPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const [receipt, setReceipt] = useState<any | null>(null);
  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      arApi.getReceipt(id).catch(() => null),
      fetch(`/api/companies`, { headers: { Authorization: `Bearer ${localStorage.getItem('accessToken')}` } })
        .then((r) => (r.ok ? r.json() : null))
        .catch(() => null),
    ])
      .then(([r, comps]) => {
        if (!r) throw new Error('السند غير موجود');
        setReceipt(r);
        const list = Array.isArray(comps) ? comps : [];
        const holding = list.find((c: Company) => c.isGroup) || list[0] || null;
        setCompany(holding);
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل التحميل.')))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) return <div className="p-8 text-center text-gray-500 no-print">جاري التحميل…</div>;
  if (error || !receipt) {
    return <div className="p-8 text-center text-red-600 no-print">{error || 'السند غير موجود.'}</div>;
  }

  const allocations = receipt.allocations || [];
  const total = Number(receipt.amount ?? 0);

  return (
    <PrintLayout documentTitle={`سند قبض ${receipt.receiptNumber || ''}`}>
      <div className="flex justify-between items-start border-b-2 border-gray-800 pb-4 mb-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">{company?.name || 'الشركة'}</h1>
          {company?.address && <p className="text-xs text-gray-500 mt-1">📍 {company.address}</p>}
          {company?.taxId && <p className="text-xs text-gray-500">🏛️ {company.taxId}</p>}
        </div>
        <div className="text-end">
          <h2 className="text-3xl font-bold text-green-700 mb-1">سند قبض</h2>
          <p className="text-sm text-gray-600">RECEIPT VOUCHER</p>
          <p className="text-sm mt-3 font-mono font-bold">رقم: {receipt.receiptNumber || receipt.id?.substring(0, 8)}</p>
        </div>
      </div>

      <div className="grid grid-cols-2 gap-6 mb-6">
        <div className="bg-gray-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-2">استلمنا من</h3>
          <p className="font-bold text-gray-900">{receipt.customerName || '—'}</p>
          {receipt.customerCode && <p className="text-xs text-gray-500 font-mono">كود: {receipt.customerCode}</p>}
        </div>
        <div className="bg-gray-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-2">تفاصيل السند</h3>
          <div className="text-sm space-y-1">
            <div className="flex justify-between"><span className="text-gray-600">التاريخ:</span><span className="font-mono">{receipt.receiptDate ? new Date(receipt.receiptDate).toLocaleDateString('en-GB') : '-'}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">العملة:</span><span className="font-mono font-semibold">{receipt.currencyCode || 'LYD'}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">طريقة الدفع:</span><span className="font-mono">{receipt.paymentMethod || '—'}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">الحالة:</span><span className="font-semibold">{receipt.status || '-'}</span></div>
          </div>
        </div>
      </div>

      <div className="bg-green-50 border-2 border-green-300 rounded-lg p-6 text-center mb-6">
        <p className="text-sm text-gray-600 mb-1">المبلغ المستلم</p>
        <p className="text-4xl font-bold text-green-700 font-mono">{formatNumber(total)} {receipt.currencyCode || 'LYD'}</p>
        <p className="text-xs text-gray-500 mt-2 italic">فقط {numberToArabicWords(total)} {currencyArabicName(receipt.currencyCode || 'LYD')} لا غير</p>
      </div>

      {allocations.length > 0 && (
        <div className="mb-6">
          <h3 className="text-sm font-bold text-gray-700 mb-2">تخصيص السند على الفواتير</h3>
          <table className="w-full border-collapse">
            <thead>
              <tr className="bg-gray-200 text-gray-700">
                <th className="text-start py-2 px-3 text-xs font-bold">رقم الفاتورة</th>
                <th className="text-start py-2 px-3 text-xs font-bold">التاريخ</th>
                <th className="text-end py-2 px-3 text-xs font-bold">إجمالي الفاتورة</th>
                <th className="text-end py-2 px-3 text-xs font-bold">المخصص من السند</th>
              </tr>
            </thead>
            <tbody>
              {allocations.map((a: any, i: number) => (
                <tr key={i} className="border-b">
                  <td className="py-2 px-3 text-sm font-mono">{a.invoiceNumber || a.salesInvoiceId?.substring(0, 8) || '—'}</td>
                  <td className="py-2 px-3 text-sm font-mono">{a.invoiceDate ? new Date(a.invoiceDate).toLocaleDateString('en-GB') : '—'}</td>
                  <td className="py-2 px-3 text-sm font-mono text-end">{formatNumber(a.invoiceTotal)}</td>
                  <td className="py-2 px-3 text-sm font-mono text-end font-bold">{formatNumber(a.amountApplied)}</td>
                </tr>
              ))}
              <tr className="bg-gray-100 font-bold">
                <td colSpan={3} className="py-2 px-3 text-end text-sm">إجمالي التخصيصات:</td>
                <td className="py-2 px-3 text-end text-sm font-mono">{formatNumber(allocations.reduce((s: number, a: any) => s + Number(a.amountApplied || 0), 0))}</td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      {receipt.notes && (
        <div className="mb-6">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-1">ملاحظات</h3>
          <p className="text-sm text-gray-700 bg-gray-50 rounded p-3">{receipt.notes}</p>
        </div>
      )}

      <div className="border-t-2 border-gray-800 pt-6 mt-12 grid grid-cols-3 gap-4 text-center text-sm text-gray-600">
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">المحاسب</div>
        </div>
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">المدير المالي</div>
        </div>
        <div>
          <div className="border-t border-gray-400 pt-2 mt-12">المستلم (العميل)</div>
        </div>
      </div>
    </PrintLayout>
  );
}

function numberToArabicWords(n: number): string {
  // بسيط — للأرقام الصغيرة. للأرقام الكبيرة يحتاج i18n library.
  if (n === 0) return 'صفر';
  const ones = ['', 'واحد', 'اثنان', 'ثلاثة', 'أربعة', 'خمسة', 'ستة', 'سبعة', 'ثمانية', 'تسعة'];
  const tens = ['', 'عشرة', 'عشرون', 'ثلاثون', 'أربعون', 'خمسون', 'ستون', 'سبعون', 'ثمانون', 'تسعون'];
  const hundreds = ['', 'مائة', 'مئتان', 'ثلاثمائة', 'أربعمائة', 'خمسمائة', 'ستمائة', 'سبعمائة', 'ثمانمائة', 'تسعمائة'];
  const intPart = Math.floor(n);
  const decPart = Math.round((n - intPart) * 1000);
  if (intPart < 10) return ones[intPart] + (decPart ? ' و' + decPart + ' ألف' : '');
  if (intPart < 100) return tens[Math.floor(intPart / 10)] + (intPart % 10 ? ' و' + ones[intPart % 10] : '');
  if (intPart < 1000) return hundreds[Math.floor(intPart / 100)] + (intPart % 100 ? ' و' + tens[Math.floor((intPart % 100) / 10)] + (intPart % 10 ? ' و' + ones[intPart % 10] : '') : '');
  return `${Math.floor(intPart / 1000)} ألف و${numberToArabicWords(intPart % 1000)}`;
}

function currencyArabicName(code: string): string {
  const map: Record<string, string> = { LYD: 'دينار ليبي', USD: 'دولار أمريكي', EUR: 'يورو' };
  return map[code] || code;
}
