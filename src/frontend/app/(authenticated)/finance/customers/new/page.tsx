'use client';

// صفحة إنشاء عميل جديد (Customer) — form

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { arApi, getErrorMessage } from '@/lib/api';

interface FormState {
  code: string;
  name: string;
  nameEn: string;
  taxId: string;
  email: string;
  phone: string;
  address: string;
  creditLimit: string;
  paymentTermsDays: string;
}

export default function NewCustomerPage() {
  const router = useRouter();
  const [form, setForm] = useState<FormState>({
    code: '',
    name: '',
    nameEn: '',
    taxId: '',
    email: '',
    phone: '',
    address: '',
    creditLimit: '',
    paymentTermsDays: '30',
  });
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onChange = <K extends keyof FormState>(k: K) => (e: React.ChangeEvent<HTMLInputElement>) => {
    setForm((f) => ({ ...f, [k]: e.target.value }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await arApi.createCustomer({
        code: form.code,
        name: form.name,
        nameEn: form.nameEn || undefined,
        taxId: form.taxId || undefined,
        email: form.email || undefined,
        phone: form.phone || undefined,
        address: form.address || undefined,
        creditLimit: form.creditLimit ? Number(form.creditLimit) : undefined,
        paymentTermsDays: Number(form.paymentTermsDays) || 30,
      });
      router.push('/finance/customers');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء العميل. تأكد من البيانات وأن الـ backend يدعم الـ endpoint.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ عميل جديد"
        description="أضف عميلاً جديداً إلى النظام"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/customers' },
          { label: 'العملاء', href: '/finance/customers' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/customers">
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
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="كود العميل *"
              value={form.code}
              onChange={onChange('code')}
              required
              placeholder="CUST-001"
            />
            <Input
              label="مدة السداد (أيام)"
              type="number"
              value={form.paymentTermsDays}
              onChange={onChange('paymentTermsDays')}
              min={0}
              max={365}
            />
          </div>

          <Input
            label="اسم العميل (بالعربية) *"
            value={form.name}
            onChange={onChange('name')}
            required
            placeholder="مثال: شركة الفجر للمقاولات"
          />

          <Input
            label="اسم العميل (بالإنجليزية)"
            value={form.nameEn}
            onChange={onChange('nameEn')}
            placeholder="Alfajr Construction Co."
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="البريد الإلكتروني"
              type="email"
              value={form.email}
              onChange={onChange('email')}
              placeholder="customer@example.com"
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
              value={form.taxId}
              onChange={onChange('taxId')}
              placeholder="اختياري"
            />
            <Input
              label="حد الائتمان (LYD)"
              type="number"
              value={form.creditLimit}
              onChange={onChange('creditLimit')}
              placeholder="0.0000"
              min={0}
            />
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ العميل
            </Button>
            <Link href="/finance/customers">
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
