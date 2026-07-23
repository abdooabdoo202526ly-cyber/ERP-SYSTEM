'use client';

// إنشاء مخزن جديد (Warehouse)

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, AlertCircle } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { companiesApi, Company, warehousesApi, getErrorMessage } from '@/lib/api';

export default function NewWarehousePage() {
  const router = useRouter();
  useAuth();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [location, setLocation] = useState('');
  const [managerUserId, setManagerUserId] = useState('');
  const [companyId, setCompanyId] = useState('');
  const [companies, setCompanies] = useState<Company[]>([]);
  const [loadingCompanies, setLoadingCompanies] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadCompanies = async () => {
      try {
        const list = await companiesApi.listCompanies(false);
        setCompanies(list.filter((c) => c.isActive));
      } catch {
        // ignore
      } finally {
        setLoadingCompanies(false);
      }
    };
    loadCompanies();
  }, []);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!companyId) {
      setError('يجب اختيار الشركة.');
      return;
    }

    setSubmitting(true);
    try {
      const created = await warehousesApi.create({
        companyId,
        code: code.trim(),
        name: name.trim(),
        location: location.trim() || undefined,
        managerUserId: managerUserId.trim() || undefined,
      });
      router.push(`/inventory/warehouses/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء المخزن.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ مخزن جديد"
        description="إنشاء مخزن جديد (Warehouse)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخازن', href: '/inventory/warehouses' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/inventory/warehouses">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
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
          <div>
            <label className="block text-sm text-gray-500 mb-1">الشركة *</label>
            <select
              value={companyId}
              onChange={(e) => setCompanyId(e.target.value)}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm bg-white"
              required
              disabled={loadingCompanies}
            >
              <option value="">
                {loadingCompanies ? 'جاري التحميل...' : '— اختر الشركة —'}
              </option>
              {companies.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.code} - {c.name}
                </option>
              ))}
            </select>
            {!loadingCompanies && companies.length === 0 && (
              <p className="text-xs text-red-500 mt-1 flex items-center gap-1">
                <AlertCircle className="h-3 w-3" /> لا توجد شركات نشطة. أنشئ شركة أولاً.
              </p>
            )}
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input
              label="الكود *"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              placeholder="WH-01"
            />
            <Input
              label="اسم المخزن *"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              placeholder="مثال: المخزن الرئيسي - طرابلس"
            />
          </div>

          <Input
            label="الموقع"
            value={location}
            onChange={(e) => setLocation(e.target.value)}
            placeholder="مثال: طرابلس - حي الزاوية"
          />

          <Input
            label="مدير المخزن (User ID)"
            value={managerUserId}
            onChange={(e) => setManagerUserId(e.target.value)}
            placeholder="اختياري — GUID للمستخدم"
          />

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
              disabled={companies.length === 0}
            >
              حفظ
            </Button>
            <Link href="/inventory/warehouses">
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
