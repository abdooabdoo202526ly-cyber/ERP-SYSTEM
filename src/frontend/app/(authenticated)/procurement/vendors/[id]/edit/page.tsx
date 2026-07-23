'use client';

// صفحة تعديل مورّد (Vendor Edit) — form مُعبّأ مسبقاً من الـ API.
// عند الحفظ: PUT /api/procurement/vendors/{id} ثم الانتقال لـ detail.

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { procurementApi, Vendor, PAYMENT_TERMS, getErrorMessage } from '@/lib/api';

interface FormState {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  currency: string;
  paymentTerms: string;
  isActive: boolean;
}

const CURRENCY_OPTIONS = [
  { label: 'دينار ليبي (LYD)', value: 'LYD' },
  { label: 'دولار أمريكي (USD)', value: 'USD' },
  { label: 'يورو (EUR)', value: 'EUR' },
  { label: 'جنيه مصري (EGP)', value: 'EGP' },
  { label: 'ريال سعودي (SAR)', value: 'SAR' },
  { label: 'درهم إماراتي (AED)', value: 'AED' },
];

const PAYMENT_TERMS_OPTIONS = Object.entries(PAYMENT_TERMS).map(([k, v]) => ({ label: v, value: k }));

export default function EditVendorPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();

  const [form, setForm] = useState<FormState | null>(null);
  const [originalCode, setOriginalCode] = useState<string>('');
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      if (!params?.id) return;
      setLoading(true);
      setError(null);
      try {
        const v: Vendor = await procurementApi.getVendor(params.id);
        setForm({
          name: v.name,
          email: v.email || '',
          phone: v.phone || '',
          address: v.address || '',
          taxNumber: v.taxNumber || '',
          currency: v.currency || 'LYD',
          paymentTerms: v.paymentTerms || 'Net30',
          isActive: v.isActive,
        });
        setOriginalCode(v.code || '');
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'تعذّر تحميل المورّد.'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params?.id]);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => {
    const value = e.target.type === 'checkbox' ? (e.target as HTMLInputElement).checked : e.target.value;
    setForm((f) => (f ? { ...f, [k]: value as FormState[K] } : f));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form) return;
    setError(null);

    if (!form.name.trim()) {
      setError('اسم المورّد مطلوب.');
      return;
    }

    setSubmitting(true);
    try {
      await procurementApi.updateVendor(params.id, {
        name: form.name,
        email: form.email || undefined,
        phone: form.phone || undefined,
        address: form.address || undefined,
        taxNumber: form.taxNumber || undefined,
        currency: form.currency,
        paymentTerms: form.paymentTerms,
        isActive: form.isActive,
      } as Partial<Vendor>);
      router.push(`/procurement/vendors/${params.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث المورّد.'));
      setSubmitting(false);
    }
  };

  if (loading || !form) {
    return (
      <div>
        <PageHeader title="تعديل مورّد" />
        <Card className="max-w-2xl">
          <div className="text-center py-12 text-gray-500">
            {error ? (
              <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
                {error}
              </div>
            ) : (
              <>
                <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
                <p className="mt-3 text-sm">جاري التحميل...</p>
              </>
            )}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل مورّد"
        description={originalCode ? `الكود: ${originalCode} (لا يمكن تغييره)` : 'تعديل بيانات المورّد'}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المشتريات', href: '/procurement/vendors' },
          { label: 'الموردين', href: '/procurement/vendors' },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/procurement/vendors/${params.id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للتفاصيل
            </Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <Input
            label="اسم المورّد *"
            value={form.name}
            onChange={onChange('name')}
            required
            placeholder="مثال: شركة الأمل للتوريدات"
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="البريد الإلكتروني"
              type="email"
              value={form.email}
              onChange={onChange('email')}
              placeholder="vendor@example.com"
            />
            <Input
              label="الهاتف"
              type="tel"
              value={form.phone}
              onChange={onChange('phone')}
              placeholder="+218 91 234 5678"
            />
          </div>

          <Input
            label="العنوان"
            value={form.address}
            onChange={onChange('address')}
            placeholder="العنوان الكامل"
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="الرقم الضريبي"
              value={form.taxNumber}
              onChange={onChange('taxNumber')}
              placeholder="اختياري"
            />
            <Select
              label="العملة"
              value={form.currency}
              onChange={onChange('currency')}
              options={CURRENCY_OPTIONS}
            />
          </div>

          <Select
            label="شروط الدفع"
            value={form.paymentTerms}
            onChange={onChange('paymentTerms')}
            options={PAYMENT_TERMS_OPTIONS}
          />

          <div className="flex items-center gap-2 pt-2">
            <input
              type="checkbox"
              id="isActive"
              checked={form.isActive}
              onChange={onChange('isActive')}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <label htmlFor="isActive" className="text-sm text-gray-700 select-none cursor-pointer">
              المورّد نشط
            </label>
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ التغييرات
            </Button>
            <Link href={`/procurement/vendors/${params.id}`}>
              <Button type="button" variant="ghost">
                إلغاء
              </Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
