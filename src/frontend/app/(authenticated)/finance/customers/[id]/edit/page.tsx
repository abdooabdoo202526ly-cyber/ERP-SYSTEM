'use client';

// صفحة تعديل العميل (Customer Edit) — form مُعبّأ مسبقاً
// يحفظ عبر customersApi.update ثم يعود لصفحة التفاصيل

import { useEffect, useState } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { customersApi, Customer, getErrorMessage } from '@/lib/api';

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
  isActive: boolean;
}

export default function EditCustomerPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const id = params?.id;

  const [form, setForm] = useState<FormState | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      if (!id) return;
      setLoading(true);
      setError(null);
      try {
        const c: Customer = await customersApi.get(id);
        setForm({
          code: c.code || '',
          name: c.name || '',
          nameEn: c.nameEn || '',
          taxId: c.taxId || '',
          email: c.email || '',
          phone: c.phone || '',
          address: c.address || '',
          creditLimit:
            c.creditLimit !== undefined && c.creditLimit !== null
              ? String(c.creditLimit)
              : '',
          paymentTermsDays: String(c.paymentTermsDays ?? 30),
          isActive: c.isActive !== false,
        });
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل تحميل العميل.'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [id]);

  const onChange = <K extends keyof FormState>(k: K) =>
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const v = k === 'isActive' ? e.target.checked : e.target.value;
      setForm((f) => (f ? { ...f, [k]: v as FormState[K] } : f));
    };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!form || !id) return;
    setError(null);
    setSubmitting(true);
    try {
      await customersApi.update(id, {
        code: form.code,
        name: form.name,
        nameEn: form.nameEn || undefined,
        taxId: form.taxId || undefined,
        email: form.email || undefined,
        phone: form.phone || undefined,
        address: form.address || undefined,
        creditLimit: form.creditLimit ? Number(form.creditLimit) : undefined,
        paymentTermsDays: Number(form.paymentTermsDays) || 30,
        isActive: form.isActive,
      });
      router.push(`/finance/customers/${id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل حفظ التعديلات. تأكد من البيانات.'));
      setSubmitting(false);
    }
  };

  if (loading || !form) {
    return (
      <div>
        <PageHeader title="تعديل عميل" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
            {error && (
              <p className="mt-3 text-sm text-red-600">{error}</p>
            )}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={`✏️ تعديل العميل: ${form.name}`}
        description={`الكود: ${form.code}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المالية', href: '/finance/customers' },
          { label: 'العملاء', href: '/finance/customers' },
          { label: form.name, href: `/finance/customers/${id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/finance/customers/${id}`}>
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
              label="حد الائتمان"
              type="number"
              value={form.creditLimit}
              onChange={onChange('creditLimit')}
              placeholder="0.0000"
              min={0}
            />
          </div>

          <label className="flex items-center gap-2 text-sm pt-2 border-t cursor-pointer">
            <input
              type="checkbox"
              checked={form.isActive}
              onChange={onChange('isActive')}
              className="rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span>العميل فعّال (يمكن إصدار فواتير وسندات قبض له)</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ التعديلات
            </Button>
            <Link href={`/finance/customers/${id}`}>
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
