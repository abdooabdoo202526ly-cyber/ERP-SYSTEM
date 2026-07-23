'use client';

// صفحة تفاصيل المورّد (Vendor Detail) — تعرض المعلومات الأساسية
// (code, name, email, phone, tax, payment terms, status) + رصيد المستحقات (open bills).
// أزرار: Edit / Deactivate.

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Pencil,
  Power,
  Mail,
  Phone,
  MapPin,
  Hash,
  Globe,
  Building2,
  FileText,
  Calendar,
  CheckCircle2,
  XCircle,
  Wallet,
} from 'lucide-react';
import { Button, Card, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { procurementApi, Vendor, VendorBill, PAYMENT_TERMS, BILL_STATUSES, getErrorMessage } from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function VendorDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();
  const [vendor, setVendor] = useState<Vendor | null>(null);
  const [bills, setBills] = useState<VendorBill[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const load = useCallback(async () => {
    if (!params?.id) return;
    setLoading(true);
    setError(null);
    try {
      const [v, allBills] = await Promise.all([
        procurementApi.getVendor(params.id),
        procurementApi.listBills().catch(() => [] as VendorBill[]),
      ]);
      setVendor(v);
      // الفواتير المفتوحة لهذا المورّد فقط: Draft + Posted (أي غير مدفوعة/ملغاة)
      const open = allBills.filter(
        (b) => b.vendorId === v.id && (b.status === 1 || b.status === 2)
      );
      setBills(open);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل بيانات المورّد.'));
    } finally {
      setLoading(false);
    }
  }, [params?.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!vendor) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إلغاء تفعيل المورّد "${vendor.name}"؟`)) return;
    setDeactivating(true);
    try {
      await procurementApi.deactivateVendor(vendor.id);
      router.push('/procurement/vendors');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
      setDeactivating(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="مورّد" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!vendor) {
    return (
      <div>
        <PageHeader
          title="مورّد"
          actions={
            <Link href="/procurement/vendors">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'المورّد غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/procurement/vendors">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  // الرصيد = مجموع الفواتير المفتوحة (Draft + Posted)
  const openBalance = bills.reduce((sum, b) => sum + (b.totalAmount || 0), 0);

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-emerald-100 text-emerald-700 flex items-center justify-center">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <span>{vendor.name}</span>
              {vendor.taxNumber && (
                <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                  TAX: {vendor.taxNumber}
                </p>
              )}
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {vendor.isActive ? (
              <Badge variant="success">نشط</Badge>
            ) : (
              <Badge variant="neutral">غير نشط</Badge>
            )}
            <Badge variant="info">{PAYMENT_TERMS[vendor.paymentTerms] || vendor.paymentTerms}</Badge>
            <span className="text-gray-400">•</span>
            <span>العملة: {vendor.currency}</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المشتريات', href: '/procurement/vendors' },
          { label: 'الموردين', href: '/procurement/vendors' },
          { label: vendor.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/procurement/vendors">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            <Link href={`/procurement/vendors/${vendor.id}/edit`}>
              <Button variant="primary" iconLeft={<Pencil className="h-4 w-4" />}>
                تعديل
              </Button>
            </Link>
            {vendor.isActive && (
              <Button
                variant="ghost"
                onClick={onDeactivate}
                loading={deactivating}
                iconLeft={<Power className="h-4 w-4 text-red-500" />}
              >
                <span className="text-red-600 text-sm">إيقاف</span>
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
              value={vendor.code || '—'}
              mono
            />
            <FieldRow
              icon={<Building2 className="h-4 w-4" />}
              label="الاسم"
              value={vendor.name}
            />
            <FieldRow
              icon={<Mail className="h-4 w-4" />}
              label="البريد الإلكتروني"
              value={vendor.email}
            />
            <FieldRow
              icon={<Phone className="h-4 w-4" />}
              label="الهاتف"
              value={vendor.phone}
              dir="ltr"
            />
            <FieldRow
              icon={<Hash className="h-4 w-4" />}
              label="الرقم الضريبي"
              value={vendor.taxNumber}
              mono
            />
            <FieldRow
              icon={<Globe className="h-4 w-4" />}
              label="العملة"
              value={vendor.currency}
              mono
            />
            <div className="md:col-span-2">
              <FieldRow
                icon={<MapPin className="h-4 w-4" />}
                label="العنوان"
                value={vendor.address}
              />
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">شروط الدفع</p>
              <Badge variant="info">{PAYMENT_TERMS[vendor.paymentTerms] || vendor.paymentTerms}</Badge>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {vendor.isActive ? (
                <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                  <CheckCircle2 className="h-4 w-4" /> نشط
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                  <XCircle className="h-4 w-4" /> غير نشط
                </span>
              )}
            </div>
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ الإنشاء"
              value={formatDateTime(vendor.createdAt)}
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="آخر تحديث"
              value={formatDateTime(vendor.updatedAt)}
            />
          </div>
        </Card>

        {/* رصيد المستحقات */}
        <Card title="💰 رصيد المستحقات">
          <div className="space-y-3">
            <div className="p-4 rounded-lg bg-amber-50 border border-amber-200">
              <div className="flex items-center gap-2 text-amber-700 text-sm font-semibold mb-1">
                <Wallet className="h-4 w-4" />
                رصيد الفواتير المفتوحة
              </div>
              <p className="text-3xl font-bold text-amber-900 mt-2">
                {formatNumber(openBalance)}
                <span className="text-base font-normal text-amber-700 ms-2">{vendor.currency}</span>
              </p>
              <p className="text-xs text-amber-600 mt-1">
                من {bills.length} فاتورة غير مسددة
              </p>
            </div>

            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">المعرّف</span>
              <span className="text-xs font-mono text-gray-700" dir="ltr">
                {vendor.id.substring(0, 8)}…
              </span>
            </div>

            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">المستأجر (Tenant)</span>
              <span className="text-xs font-mono text-gray-700" dir="ltr">
                {vendor.tenantId.substring(0, 8)}…
              </span>
            </div>
          </div>
        </Card>
      </div>

      {/* الفواتير المفتوحة */}
      <Card
        className="mt-4"
        title={
          <span>
            <FileText className="h-4 w-4 inline-block me-2" />
            فواتير مفتوحة
            {bills.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({bills.length})
              </span>
            )}
          </span>
        }
      >
        {bills.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <FileText className="h-10 w-10 mx-auto text-gray-300 mb-2" />
            <p className="text-sm">لا توجد فواتير مفتوحة لهذا المورّد.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead>
                <tr className="text-right text-xs text-gray-500 border-b">
                  <th className="py-2 pr-2">رقم الفاتورة</th>
                  <th className="py-2 pr-2">الحالة</th>
                  <th className="py-2 pr-2">التاريخ</th>
                  <th className="py-2 pr-2">الاستحقاق</th>
                  <th className="py-2 pr-2 text-left">الإجمالي</th>
                </tr>
              </thead>
              <tbody>
                {bills.map((b) => (
                  <tr key={b.id} className="border-b hover:bg-gray-50">
                    <td className="py-2 pr-2">
                      <Link
                        href={`/procurement/bills/${b.id}`}
                        className="font-mono text-blue-600 hover:underline"
                      >
                        {b.billNumber}
                      </Link>
                    </td>
                    <td className="py-2 pr-2">
                      <Badge variant={b.status === 2 ? 'info' : 'neutral'}>
                        {BILL_STATUSES[b.status] || b.status}
                      </Badge>
                    </td>
                    <td className="py-2 pr-2 text-gray-700">{formatDate(b.billDate)}</td>
                    <td className="py-2 pr-2 text-gray-700">{formatDate(b.dueDate)}</td>
                    <td className="py-2 pr-2 text-left font-mono font-semibold">
                      {formatNumber(b.totalAmount)} {b.currency}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </Card>
    </div>
  );
}

// ============ Local subcomponents ============

interface FieldRowProps {
  icon: React.ReactNode;
  label: string;
  value?: string | null;
  mono?: boolean;
  dir?: 'ltr' | 'rtl';
}

function FieldRow({ icon, label, value, mono, dir }: FieldRowProps) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
        {icon} {label}
      </p>
      <p
        className={`text-sm ${mono ? 'font-mono' : ''} ${!value ? 'text-gray-400' : ''}`}
        dir={dir}
      >
        {value || '—'}
      </p>
    </div>
  );
}
