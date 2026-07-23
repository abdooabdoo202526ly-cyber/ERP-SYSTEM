'use client';

// إنشاء شركة جديدة — Holding أو Subsidiary تحت Holding

import { Suspense, useEffect, useState } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Building2, Layers } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { companiesApi, Company, getErrorMessage } from '@/lib/api';

type CompanyKind = 'holding' | 'subsidiary';

interface FormState {
  kind: CompanyKind;
  code: string;
  name: string;
  legalName: string;
  baseCurrency: string;
  parentCompanyId: string;
}

function NewCompanyForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  useAuth();

  // ?type=holding | subsidiary&parent=<id> — لتوجيه النوع مسبقاً من صفحة أخرى
  const initialKind: CompanyKind =
    searchParams?.get('type') === 'subsidiary' ? 'subsidiary' : 'holding';
  const initialParent = searchParams?.get('parent') || '';

  const [form, setForm] = useState<FormState>({
    kind: initialKind,
    code: '',
    name: '',
    legalName: '',
    baseCurrency: 'LYD',
    parentCompanyId: initialParent,
  });
  const [holdings, setHoldings] = useState<Company[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // تحميل قائمة الـ Holdings عند الحاجة (للـ subsidiary)
  useEffect(() => {
    if (form.kind !== 'subsidiary') return;
    const loadHoldings = async () => {
      try {
        const list = await companiesApi.listCompanies(false);
        // فقط الـ Holdings النشطة تكون صالحة كأب
        setHoldings(list.filter((c) => c.isGroup && c.isActive));
      } catch {
        // ignore
      }
    };
    loadHoldings();
  }, [form.kind]);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      let created: Company;
      if (form.kind === 'holding') {
        created = await companiesApi.createHolding({
          code: form.code,
          name: form.name,
          legalName: form.legalName || form.name,
          baseCurrency: form.baseCurrency,
        });
      } else {
        if (!form.parentCompanyId) {
          throw new Error('يجب اختيار الشركة القابضة الأم.');
        }
        created = await companiesApi.addSubsidiary({
          parentCompanyId: form.parentCompanyId,
          code: form.code,
          name: form.name,
          legalName: form.legalName || undefined,
        });
      }
      router.push(`/admin/companies/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الشركة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ شركة جديدة"
        description={
          form.kind === 'holding'
            ? 'إنشاء شركة قابضة (Holding) — جذر شجرة الشركات'
            : 'إنشاء شركة تابعة (Subsidiary) تحت شركة قابضة'
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الشركات', href: '/admin/companies' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/admin/companies">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {/* نوع الشركة — tabs */}
        <div className="mb-6">
          <label className="block text-sm text-gray-500 mb-2">نوع الشركة</label>
          <div className="grid grid-cols-2 gap-2">
            <button
              type="button"
              onClick={() => setForm((f) => ({ ...f, kind: 'holding', parentCompanyId: '' }))}
              className={`flex items-center gap-3 p-3 rounded-lg border-2 transition-colors text-right ${
                form.kind === 'holding'
                  ? 'border-purple-500 bg-purple-50'
                  : 'border-gray-200 hover:border-gray-300'
              }`}
            >
              <div
                className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                  form.kind === 'holding' ? 'bg-purple-200 text-purple-700' : 'bg-gray-100 text-gray-500'
                }`}
              >
                <Layers className="h-5 w-5" />
              </div>
              <div>
                <p className="font-bold text-sm">Holding (قابضة)</p>
                <p className="text-xs text-gray-500">جذر شجرة الشركات</p>
              </div>
            </button>
            <button
              type="button"
              onClick={() => setForm((f) => ({ ...f, kind: 'subsidiary' }))}
              className={`flex items-center gap-3 p-3 rounded-lg border-2 transition-colors text-right ${
                form.kind === 'subsidiary'
                  ? 'border-blue-500 bg-blue-50'
                  : 'border-gray-200 hover:border-gray-300'
              }`}
            >
              <div
                className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                  form.kind === 'subsidiary' ? 'bg-blue-200 text-blue-700' : 'bg-gray-100 text-gray-500'
                }`}
              >
                <Building2 className="h-5 w-5" />
              </div>
              <div>
                <p className="font-bold text-sm">Subsidiary (تابعة)</p>
                <p className="text-xs text-gray-500">تابعة لشركة قابضة</p>
              </div>
            </button>
          </div>
        </div>

        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          {/* Holding Parent (للـ subsidiary فقط) */}
          {form.kind === 'subsidiary' && (
            <div>
              <label className="block text-sm text-gray-500 mb-1">
                الشركة القابضة (الأم) *
              </label>
              <select
                value={form.parentCompanyId}
                onChange={onChange('parentCompanyId')}
                className="w-full border border-gray-300 rounded px-3 py-2 text-sm bg-white"
                required
              >
                <option value="">— اختر الشركة القابضة —</option>
                {holdings.map((h) => (
                  <option key={h.id} value={h.id}>
                    {h.code} - {h.name} ({h.baseCurrency})
                  </option>
                ))}
              </select>
              {holdings.length === 0 && (
                <p className="text-xs text-red-500 mt-1">
                  لا توجد شركات قابضة متاحة. أنشئ Holding أولاً.
                </p>
              )}
            </div>
          )}

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="الكود *"
              value={form.code}
              onChange={onChange('code')}
              required
              placeholder={form.kind === 'holding' ? '000' : 'SUB-001'}
            />
            <Input
              label="الاسم *"
              value={form.name}
              onChange={onChange('name')}
              required
              placeholder="مثال: شركة الأمل القابضة"
            />
          </div>

          <Input
            label="الاسم القانوني"
            value={form.legalName}
            onChange={onChange('legalName')}
            placeholder="اختياري — الاسم المسجل رسمياً"
          />

          {form.kind === 'holding' && (
            <div>
              <label className="block text-sm text-gray-500 mb-1">
                العملة الأساسية (ISO 4217) *
              </label>
              <select
                value={form.baseCurrency}
                onChange={onChange('baseCurrency')}
                className="w-full border border-gray-300 rounded px-3 py-2 text-sm bg-white"
                required
              >
                <option value="LYD">LYD — دينار ليبي</option>
                <option value="USD">USD — دولار أمريكي</option>
                <option value="EUR">EUR — يورو</option>
                <option value="SAR">SAR — ريال سعودي</option>
                <option value="AED">AED — درهم إماراتي</option>
                <option value="EGP">EGP — جنيه مصري</option>
                <option value="GBP">GBP — جنيه إسترليني</option>
              </select>
              <p className="text-xs text-gray-400 mt-1">
                تنتقل هذه العملة للشركات التابعة عند إنشائها.
              </p>
            </div>
          )}

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ
            </Button>
            <Link href="/admin/companies">
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

export default function NewCompanyPage() {
  return (
    <Suspense fallback={<div className="text-center py-12 text-gray-500">جاري التحميل...</div>}>
      <NewCompanyForm />
    </Suspense>
  );
}
