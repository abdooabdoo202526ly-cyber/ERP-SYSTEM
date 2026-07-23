'use client';

// طباعة فاتورة المبيعات (Sales Invoice Print)
// v1.0.32: قالب رسمي مع شعار الشركة، تفاصيل العميل، البنود، الإجمالي، QR placeholder

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { PrintLayout } from '@/components/print/PrintLayout';
import { arApi, Company, getErrorMessage } from '@/lib/api';
import { formatNumber } from '@/lib/format';

export default function PrintSalesInvoicePage() {
  const params = useParams<{ id: string }>();
  const id = params?.id;
  const [invoice, setInvoice] = useState<any | null>(null);
  const [company, setCompany] = useState<Company | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    Promise.all([
      arApi.getInvoice(id).catch(() => null),
      // @ts-ignore — companiesApi may not exist in this context
      fetch(`/api/companies`, { headers: { Authorization: `Bearer ${localStorage.getItem('accessToken')}` } })
        .then((r) => (r.ok ? r.json() : null))
        .catch(() => null),
    ])
      .then(([inv, comps]) => {
        if (!inv) throw new Error('الفاتورة غير موجودة');
        setInvoice(inv);
        // أول holding company كـ "شركة المُصدِر" الافتراضية
        const list = Array.isArray(comps) ? comps : [];
        const holding = list.find((c: Company) => c.isGroup) || list[0] || null;
        setCompany(holding);
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل التحميل.')))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return <div className="p-8 text-center text-gray-500 no-print">جاري التحميل…</div>;
  }
  if (error || !invoice) {
    return (
      <div className="p-8 text-center text-red-600 no-print">
        {error || 'الفاتورة غير موجودة.'}
      </div>
    );
  }

  // معلومات الفاتورة
  const lines = invoice.lines || [];
  const subtotal = Number(invoice.subTotal ?? invoice.totalAmount ?? 0);
  const taxAmount = Number(invoice.taxAmount ?? 0);
  const total = Number(invoice.totalAmount ?? 0);
  const paid = Number(invoice.amountPaid ?? 0);
  const outstanding = Number(invoice.outstanding ?? total - paid);

  // v1.0.32: QR Placeholder — يمكن ربطه مع Libya ETA في v1.0.34
  const qrPlaceholder = `INV:${invoice.invoiceNumber || invoice.id}|TOTAL:${total}|DATE:${invoice.invoiceDate || ''}`;

  return (
    <PrintLayout documentTitle={`فاتورة ${invoice.invoiceNumber || ''}`}>
      {/* Header: Company info + Invoice number */}
      <div className="flex justify-between items-start border-b-2 border-gray-800 pb-6 mb-6">
        <div className="flex-1">
          <h1 className="text-2xl font-bold text-gray-900">
            {company?.name || 'الشركة المُصدِرة'}
          </h1>
          {company?.legalName && (
            <p className="text-sm text-gray-600 mt-0.5">{company.legalName}</p>
          )}
          <div className="text-xs text-gray-500 mt-2 space-y-0.5">
            {company?.address && <p>📍 {company.address}</p>}
            {company?.phone && <p>📞 {company.phone}</p>}
            {company?.email && <p>✉️ {company.email}</p>}
            {company?.taxId && <p>🏛️ الرقم الضريبي: {company.taxId}</p>}
          </div>
        </div>
        <div className="text-end">
          <h2 className="text-3xl font-bold text-blue-700 mb-1">فاتورة ضريبية</h2>
          <p className="text-sm text-gray-600">TAX INVOICE</p>
          <div className="mt-3 text-sm">
            <p><span className="text-gray-600">رقم الفاتورة:</span> <span className="font-mono font-bold">{invoice.invoiceNumber || invoice.id?.substring(0, 8)}</span></p>
            <p><span className="text-gray-600">التاريخ:</span> <span className="font-mono">{invoice.invoiceDate ? new Date(invoice.invoiceDate).toLocaleDateString('en-GB') : '-'}</span></p>
            {invoice.dueDate && (
              <p><span className="text-gray-600">تاريخ الاستحقاق:</span> <span className="font-mono">{new Date(invoice.dueDate).toLocaleDateString('en-GB')}</span></p>
            )}
            <p><span className="text-gray-600">الحالة:</span> <span className="font-semibold">{invoice.status || '-'}</span></p>
          </div>
        </div>
      </div>

      {/* Customer + Company info (Bill To) */}
      <div className="grid grid-cols-2 gap-6 mb-6">
        <div className="bg-gray-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-2">فاتورة إلى</h3>
          <p className="font-bold text-gray-900">{invoice.customerName || '—'}</p>
          {invoice.customerCode && <p className="text-xs text-gray-500 font-mono">كود: {invoice.customerCode}</p>}
          {invoice.customerAddress && <p className="text-sm text-gray-600 mt-1">📍 {invoice.customerAddress}</p>}
          {invoice.customerTaxId && <p className="text-xs text-gray-500 mt-1">الرقم الضريبي: {invoice.customerTaxId}</p>}
        </div>
        <div className="bg-gray-50 rounded-lg p-4">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-2">تفاصيل الفاتورة</h3>
          <div className="text-sm space-y-1">
            <div className="flex justify-between"><span className="text-gray-600">العملة:</span><span className="font-mono font-semibold">{invoice.currencyCode || 'LYD'}</span></div>
            <div className="flex justify-between"><span className="text-gray-600">طريقة الدفع:</span><span className="font-mono">{invoice.paymentMethod || '—'}</span></div>
            {invoice.poNumber && (
              <div className="flex justify-between"><span className="text-gray-600">رقم أمر الشراء:</span><span className="font-mono">{invoice.poNumber}</span></div>
            )}
          </div>
        </div>
      </div>

      {/* Line items table */}
      <table className="w-full mb-6 border-collapse">
        <thead>
          <tr className="bg-gray-800 text-white">
            <th className="text-start py-2 px-3 text-xs font-bold uppercase w-10">#</th>
            <th className="text-start py-2 px-3 text-xs font-bold uppercase">الصنف / الخدمة</th>
            <th className="text-end py-2 px-3 text-xs font-bold uppercase w-20">الكمية</th>
            <th className="text-end py-2 px-3 text-xs font-bold uppercase w-28">سعر الوحدة</th>
            <th className="text-end py-2 px-3 text-xs font-bold uppercase w-24">خصم</th>
            <th className="text-end py-2 px-3 text-xs font-bold uppercase w-28">الإجمالي</th>
          </tr>
        </thead>
        <tbody>
          {lines.length === 0 ? (
            <tr>
              <td colSpan={6} className="py-8 text-center text-gray-400 text-sm border-b">لا توجد بنود</td>
            </tr>
          ) : (
            lines.map((l: any, i: number) => (
              <tr key={i} className={i % 2 === 0 ? 'bg-white' : 'bg-gray-50'}>
                <td className="py-2 px-3 text-sm text-center">{i + 1}</td>
                <td className="py-2 px-3 text-sm">
                  <div className="font-semibold">{l.itemName || l.description || '—'}</div>
                  {l.itemCode && <div className="text-xs text-gray-500 font-mono">{l.itemCode}</div>}
                </td>
                <td className="py-2 px-3 text-sm font-mono text-end">{l.quantity}</td>
                <td className="py-2 px-3 text-sm font-mono text-end">{formatNumber(l.unitPrice)}</td>
                <td className="py-2 px-3 text-sm font-mono text-end">{l.discountPercent ? `${l.discountPercent}%` : '—'}</td>
                <td className="py-2 px-3 text-sm font-mono text-end font-bold">{formatNumber(l.lineTotal ?? (l.quantity * l.unitPrice))}</td>
              </tr>
            ))
          )}
        </tbody>
      </table>

      {/* Totals */}
      <div className="flex justify-end mb-6">
        <div className="w-72">
          <div className="flex justify-between py-1 text-sm">
            <span className="text-gray-600">المجموع الفرعي:</span>
            <span className="font-mono">{formatNumber(subtotal)} {invoice.currencyCode || 'LYD'}</span>
          </div>
          {taxAmount > 0 && (
            <div className="flex justify-between py-1 text-sm">
              <span className="text-gray-600">الضريبة:</span>
              <span className="font-mono">{formatNumber(taxAmount)} {invoice.currencyCode || 'LYD'}</span>
            </div>
          )}
          <div className="flex justify-between py-2 border-t-2 border-b-2 border-gray-800 mt-2 font-bold text-lg">
            <span>الإجمالي:</span>
            <span className="font-mono">{formatNumber(total)} {invoice.currencyCode || 'LYD'}</span>
          </div>
          {paid > 0 && (
            <div className="flex justify-between py-1 text-sm text-green-700">
              <span>المدفوع:</span>
              <span className="font-mono">{formatNumber(paid)} {invoice.currencyCode || 'LYD'}</span>
            </div>
          )}
          {outstanding > 0 && (
            <div className="flex justify-between py-1 text-sm text-red-700 font-bold">
              <span>المتبقي:</span>
              <span className="font-mono">{formatNumber(outstanding)} {invoice.currencyCode || 'LYD'}</span>
            </div>
          )}
        </div>
      </div>

      {/* Notes */}
      {invoice.notes && (
        <div className="mb-6">
          <h3 className="text-xs font-bold text-gray-500 uppercase mb-1">ملاحظات</h3>
          <p className="text-sm text-gray-700 bg-gray-50 rounded p-3">{invoice.notes}</p>
        </div>
      )}

      {/* Footer: signatures + QR + stamps */}
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

      {/* QR placeholder for E-Invoicing (v1.0.34) */}
      <div className="text-center mt-8 text-xs text-gray-400 no-print">
        <p>سيتم تفعيل E-Invoicing Libya (QR + TLV) في v1.0.34</p>
        <p className="font-mono mt-1">{qrPlaceholder}</p>
      </div>
    </PrintLayout>
  );
}
