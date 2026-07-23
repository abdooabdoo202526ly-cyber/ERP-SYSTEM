'use client';

// صفحة تفاصيل العميل (Customer Detail) — يعرض كل بيانات العميل
// + قائمة فواتير المبيعات + الرصيد المستحق + أزرار تعديل/إيقاف/تفعيل

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Edit,
  Power,
  PowerOff,
  User as UserIcon,
  Hash,
  Mail,
  Phone,
  MapPin,
  CreditCard,
  FileText,
  Calendar,
  CheckCircle2,
  XCircle,
  AlertTriangle,
  Plus,
} from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  customersApi,
  arApi,
  Customer,
  SalesInvoice,
  SALES_INVOICE_STATUSES,
  SALES_INVOICE_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

// ============ Helpers ============

interface FieldRowProps {
  icon: React.ReactNode;
  label: string;
  value: React.ReactNode;
  mono?: boolean;
}

function FieldRow({ icon, label, value, mono }: FieldRowProps) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
        {icon} {label}
      </p>
      <p className={`text-sm break-words ${mono ? 'font-mono' : ''}`}>
        {value || <span className="text-gray-400">—</span>}
      </p>
    </div>
  );
}

// ============ Page ============

export default function CustomerDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();

  const id = params?.id;

  const [customer, setCustomer] = useState<Customer | null>(null);
  const [invoices, setInvoices] = useState<SalesInvoice[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    setError(null);
    try {
      // جلب العميل + فواتيره بالتوازي (الـ list endpoint لا يدعم customerId حالياً في BE،
      // فنفلتر يدوياً في الواجهة).
      const [c, allInvoices] = await Promise.all([
        customersApi.get(id),
        arApi.listInvoices().catch(() => [] as SalesInvoice[]),
      ]);
      setCustomer(c);
      setInvoices(allInvoices.filter((inv) => inv.customerId === c.id));
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل العميل.'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!customer) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف العميل "${customer.name}"؟`)) return;
    setActionLoading(true);
    try {
      await customersApi.deactivate(customer.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف العميل.'));
    } finally {
      setActionLoading(false);
    }
  };

  const onReactivate = async () => {
    if (!customer) return;
    setActionLoading(true);
    try {
      await customersApi.update(customer.id, { isActive: true });
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تفعيل العميل.'));
    } finally {
      setActionLoading(false);
    }
  };

  // ============ Derived stats ============

  const totalInvoiced = invoices.reduce((s, i) => s + (i.totalAmount || 0), 0);
  const totalPaid = invoices.reduce((s, i) => s + (i.paidAmount || 0), 0);
  const totalOutstanding = invoices.reduce((s, i) => s + (i.outstanding || 0), 0);
  const overdueCount = invoices.filter((i) => i.status === 5).length;
  const openInvoices = invoices.filter((i) => i.outstanding > 0 && i.status !== 6);

  // ============ Render states ============

  if (loading) {
    return (
      <div>
        <PageHeader title="عميل" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!customer) {
    return (
      <div>
        <PageHeader
          title="عميل"
          actions={
            <Link href="/finance/customers">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'العميل غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/finance/customers">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div
              className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                customer.isActive
                  ? 'bg-blue-100 text-blue-700'
                  : 'bg-gray-100 text-gray-400'
              }`}
            >
              <UserIcon className="h-5 w-5" />
            </div>
            <div>
              <span>{customer.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {customer.code}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {customer.isActive ? (
              <Badge variant="success">فعّال</Badge>
            ) : (
              <Badge variant="warning">معطّل</Badge>
            )}
            <span className="text-gray-400">•</span>
            <span>شروط الدفع: {customer.paymentTermsDays} يوم</span>
            {customer.creditLimit ? (
              <>
                <span className="text-gray-400">•</span>
                <span>حد الائتمان: {formatNumber(customer.creditLimit)}</span>
              </>
            ) : null}
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/customers' },
          { label: 'العملاء', href: '/finance/customers' },
          { label: customer.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/finance/customers">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            <Link href={`/finance/customers/${customer.id}/edit`}>
              <Button variant="primary" iconLeft={<Edit className="h-4 w-4" />}>
                تعديل
              </Button>
            </Link>
            {customer.isActive ? (
              <Button
                variant="ghost"
                onClick={onDeactivate}
                loading={actionLoading}
                iconLeft={<Power className="h-4 w-4 text-red-500" />}
              >
                <span className="text-red-600 text-sm">إيقاف</span>
              </Button>
            ) : (
              <Button
                variant="ghost"
                onClick={onReactivate}
                loading={actionLoading}
                iconLeft={<PowerOff className="h-4 w-4 text-green-500" />}
              >
                <span className="text-green-600 text-sm">تفعيل</span>
              </Button>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* المعلومات الأساسية */}
        <Card title="📋 المعلومات الأساسية" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <FieldRow
              icon={<Hash className="h-4 w-4" />}
              label="الكود"
              value={customer.code}
              mono
            />
            <FieldRow
              icon={<UserIcon className="h-4 w-4" />}
              label="الاسم بالعربية"
              value={customer.name}
            />
            <FieldRow
              icon={<UserIcon className="h-4 w-4" />}
              label="الاسم بالإنجليزية"
              value={customer.nameEn}
            />
            <FieldRow
              icon={<Hash className="h-4 w-4" />}
              label="الرقم الضريبي"
              value={customer.taxId}
              mono
            />
            <FieldRow
              icon={<Mail className="h-4 w-4" />}
              label="البريد الإلكتروني"
              value={customer.email}
              mono
            />
            <FieldRow
              icon={<Phone className="h-4 w-4" />}
              label="الهاتف"
              value={customer.phone}
              mono
            />
            <div className="md:col-span-2">
              <FieldRow
                icon={<MapPin className="h-4 w-4" />}
                label="العنوان"
                value={customer.address}
              />
            </div>
            <FieldRow
              icon={<CreditCard className="h-4 w-4" />}
              label="حد الائتمان"
              value={
                customer.creditLimit
                  ? formatNumber(customer.creditLimit)
                  : null
              }
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="شروط الدفع"
              value={`${customer.paymentTermsDays} يوم`}
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ التسجيل"
              value={formatDateTime((customer as Customer & { createdAt?: string }).createdAt)}
            />
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {customer.isActive ? (
                <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                  <CheckCircle2 className="h-4 w-4" /> فعّال
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                  <XCircle className="h-4 w-4" /> معطّل
                </span>
              )}
            </div>
          </div>
        </Card>

        {/* الإحصائيات المالية */}
        <Card title="📊 إحصائيات مالية">
          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 rounded-lg bg-blue-50">
              <span className="text-sm text-gray-600">عدد الفواتير</span>
              <span className="text-2xl font-bold text-blue-700">{invoices.length}</span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">إجمالي مفوتر</span>
              <span className="font-mono font-semibold text-gray-800">
                {formatNumber(totalInvoiced)}
              </span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-green-50">
              <span className="text-sm text-gray-600">إجمالي مدفوع</span>
              <span className="font-mono font-semibold text-green-700">
                {formatNumber(totalPaid)}
              </span>
            </div>
            <div
              className={`flex items-center justify-between p-3 rounded-lg ${
                totalOutstanding > 0 ? 'bg-red-50' : 'bg-gray-50'
              }`}
            >
              <span className="text-sm text-gray-600 flex items-center gap-1">
                {totalOutstanding > 0 && <AlertTriangle className="h-3 w-3 text-red-500" />}
                الرصيد المستحق
              </span>
              <span
                className={`font-mono font-bold ${
                  totalOutstanding > 0 ? 'text-red-700' : 'text-gray-500'
                }`}
              >
                {formatNumber(totalOutstanding)}
              </span>
            </div>
            {overdueCount > 0 && (
              <div className="flex items-center justify-between p-3 rounded-lg bg-orange-50">
                <span className="text-sm text-gray-600">فواتير متأخرة</span>
                <span className="text-lg font-bold text-orange-700">{overdueCount}</span>
              </div>
            )}
            <div className="pt-2 border-t">
              <p className="text-xs text-gray-500">المعرّف</p>
              <p className="text-xs font-mono text-gray-700" dir="ltr">
                {customer.id}
              </p>
            </div>
          </div>
        </Card>
      </div>

      {/* فواتير المبيعات */}
      <Card
        className="mt-4"
        title={
          <span>
            🧾 فواتير المبيعات
            {invoices.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({invoices.length})
              </span>
            )}
          </span>
        }
        actions={
          <Link href={`/finance/sales-invoices/new?customerId=${customer.id}`}>
            <Button variant="primary" size="sm" iconLeft={<Plus className="h-4 w-4" />}>
              فاتورة جديدة
            </Button>
          </Link>
        }
      >
        {invoices.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <FileText className="h-10 w-10 mx-auto text-gray-300 mb-2" />
            <p className="text-sm">لا توجد فواتير لهذا العميل.</p>
            <p className="text-xs mt-1 text-gray-400">
              ابدأ بإنشاء فاتورة جديدة من الزر أعلاه.
            </p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500 border-b">
                  <th className="py-2 pr-2">رقم الفاتورة</th>
                  <th className="py-2 pr-2">التاريخ</th>
                  <th className="py-2 pr-2">الاستحقاق</th>
                  <th className="py-2 pr-2 text-left">الإجمالي</th>
                  <th className="py-2 pr-2 text-left">المدفوع</th>
                  <th className="py-2 pr-2 text-left">المتبقي</th>
                  <th className="py-2 pr-2 text-center">الحالة</th>
                </tr>
              </thead>
              <tbody>
                {invoices
                  .slice()
                  .sort((a, b) => (a.invoiceDate < b.invoiceDate ? 1 : -1))
                  .slice(0, 50)
                  .map((inv) => {
                    const variant = SALES_INVOICE_STATUS_VARIANTS[inv.status] || 'neutral';
                    const statusLabel = SALES_INVOICE_STATUSES[inv.status] || '—';
                    return (
                      <tr key={inv.id} className="border-b hover:bg-gray-50">
                        <td className="py-2 pr-2">
                          <Link
                            href={`/finance/sales-invoices/${inv.id}`}
                            className="font-mono font-semibold text-blue-600 hover:underline"
                          >
                            {inv.invoiceNumber}
                          </Link>
                        </td>
                        <td className="py-2 pr-2 text-gray-600">
                          {formatDate(inv.invoiceDate)}
                        </td>
                        <td className="py-2 pr-2 text-gray-600">
                          {formatDate(inv.dueDate)}
                        </td>
                        <td className="py-2 pr-2 text-left font-mono">
                          {formatNumber(inv.totalAmount)} {inv.currencyCode}
                        </td>
                        <td className="py-2 pr-2 text-left font-mono text-green-700">
                          {formatNumber(inv.paidAmount)}
                        </td>
                        <td
                          className={`py-2 pr-2 text-left font-mono font-semibold ${
                            inv.outstanding > 0 ? 'text-red-700' : 'text-gray-500'
                          }`}
                        >
                          {formatNumber(inv.outstanding)}
                        </td>
                        <td className="py-2 pr-2 text-center">
                          <Badge variant={variant}>{statusLabel}</Badge>
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
              <tfoot>
                <tr className="border-t-2 border-gray-300 font-semibold bg-gray-50">
                  <td className="py-2 pr-2" colSpan={3}>
                    الإجمالي ({invoices.length} فاتورة)
                  </td>
                  <td className="py-2 pr-2 text-left font-mono">{formatNumber(totalInvoiced)}</td>
                  <td className="py-2 pr-2 text-left font-mono text-green-700">
                    {formatNumber(totalPaid)}
                  </td>
                  <td
                    className={`py-2 pr-2 text-left font-mono ${
                      totalOutstanding > 0 ? 'text-red-700' : 'text-gray-500'
                    }`}
                  >
                    {formatNumber(totalOutstanding)}
                  </td>
                  <td />
                </tr>
              </tfoot>
            </table>
            {invoices.length > 50 && (
              <p className="text-xs text-gray-500 mt-2 text-center">
                يعرض أول 50 فاتورة من أصل {invoices.length}
              </p>
            )}
          </div>
        )}
      </Card>
    </div>
  );
}
