'use client';

// تعديل شركة — الاسم، الاسم القانوني، الضرائب، الهاتف، البريد، العنوان، الحالة

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Building2 } from 'lucide-react';
import { Button, Card, Input, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { companiesApi, Company, getErrorMessage } from '@/lib/api';

export default function EditCompanyPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const id = params?.id;
  useAuth();

  const [company, setCompany] = useState<Company | null>(null);
  const [name, setName] = useState('');
  const [legalName, setLegalName] = useState('');
  const [taxId, setTaxId] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [address, setAddress] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!id) return;
    setLoading(true);
    companiesApi
      .getCompany(id)
      .then((c: Company) => {
        setCompany(c);
        setName(c.name || '');
        setLegalName(c.legalName || '');
        setTaxId(c.taxId || '');
        setPhone(c.phone || '');
        setEmail(c.email || '');
        setAddress(c.address || '');
        setIsActive(c.isActive);
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل تحميل الشركة.')))
      .finally(() => setLoading(false));
  }, [id]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!name.trim()) {
      setError('اسم الشركة مطلوب.');
      return;
    }
    if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      setError('البريد الإلكتروني غير صالح.');
      return;
    }

    setSubmitting(true);
    try {
      await companiesApi.updateCompany(id!, {
        name: name.trim(),
        legalName: legalName.trim() || undefined,
        taxId: taxId.trim() || undefined,
        phone: phone.trim() || undefined,
        email: email.trim() || undefined,
        address: address.trim() || undefined,
        isActive,
      });
      router.push(`/admin/companies/${id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الشركة.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return <div className="p-8 text-center text-gray-500">جاري التحميل…</div>;
  }

  if (!company) {
    return (
      <div className="p-8 text-center text-red-600">
        {error || 'لم يتم العثور على الشركة.'}
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل شركة"
        description={`الكود: ${company.code} • ${company.isGroup ? 'Holding' : 'Subsidiary'}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الشركات', href: '/admin/companies' },
          { label: company.name },
        ]}
        actions={
          <Link href={`/admin/companies/${id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4 max-w-3xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error}
          </div>
        )}

        <Card title="📋 معلومات أساسية">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="اسم الشركة *"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              placeholder="مثال: شركة الفجر القابضة"
            />
            <Input
              label="الاسم القانوني"
              value={legalName}
              onChange={(e) => setLegalName(e.target.value)}
              placeholder="الاسم المسجل رسمياً"
            />
          </div>
        </Card>

        <Card title="📞 معلومات الاتصال">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="الرقم الضريبي"
              value={taxId}
              onChange={(e) => setTaxId(e.target.value)}
              placeholder="مثال: 123456789"
            />
            <Input
              label="الهاتف"
              value={phone}
              onChange={(e) => setPhone(e.target.value)}
              placeholder="+218 91 234 5678"
            />
            <Input
              label="البريد الإلكتروني"
              type="email"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              placeholder="info@company.ly"
            />
            <Input
              label="العنوان"
              value={address}
              onChange={(e) => setAddress(e.target.value)}
              placeholder="طرابلس، ليبيا"
            />
          </div>
        </Card>

        <Card title="⚙️ الحالة">
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-blue-600"
            />
            <span>شركة نشطة (غير نشطة = مخفية من القوائم)</span>
          </label>
        </Card>

        <div className="flex items-center gap-2 pt-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
          >
            حفظ التغييرات
          </Button>
          <Link href={`/admin/companies/${id}`}>
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
