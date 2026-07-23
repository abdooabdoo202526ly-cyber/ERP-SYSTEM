'use client';

// صفحة إنشاء مورّد جديد (Vendor) — form

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Select, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { procurementApi, PAYMENT_TERMS, getErrorMessage } from '@/lib/api';

interface FormState {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxNumber: string;
  currency: string;
  paymentTerms: string;
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

export default function NewVendorPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    name: '',
    email: '',
    phone: '',
    address: '',
    taxNumber: '',
    currency: 'LYD',
    paymentTerms: 'Net30',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await procurementApi.createVendor({
        ...form,
        isActive: true,
      });
      router.push('/procurement/vendors');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء المورّد. تأكد من البيانات وأن الـ backend يدعم الـ endpoint.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ مورّد جديد"
        description="أضف مورّداً جديداً إلى النظام"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المشتريات', href: '/procurement/vendors' },
          { label: 'الموردين', href: '/procurement/vendors' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/procurement/vendors">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع للقائمة
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

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ المورّد
            </Button>
            <Link href="/procurement/vendors">
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
