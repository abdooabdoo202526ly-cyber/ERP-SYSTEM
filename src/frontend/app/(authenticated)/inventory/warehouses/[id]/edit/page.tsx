'use client';

// تعديل مخزن (Warehouse Edit)

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { warehousesApi, Warehouse, getErrorMessage } from '@/lib/api';

export default function EditWarehousePage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const [warehouse, setWarehouse] = useState<Warehouse | null>(null);
  const [name, setName] = useState('');
  const [location, setLocation] = useState('');
  const [managerUserId, setManagerUserId] = useState('');
  const [isActive, setIsActive] = useState(true);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const w = await warehousesApi.get(params.id);
      setWarehouse(w);
      setName(w.name);
      setLocation(w.location || '');
      setManagerUserId(w.managerUserId || '');
      setIsActive(w.isActive);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل المخزن.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!warehouse) return;
    setError(null);
    setSubmitting(true);
    try {
      const updated = await warehousesApi.update(warehouse.id, {
        name: name.trim(),
        location: location.trim() || undefined,
        managerUserId: managerUserId.trim() || undefined,
        isActive,
      });
      router.push(`/inventory/warehouses/${updated.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث المخزن.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="تعديل مخزن" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!warehouse) {
    return (
      <div>
        <PageHeader
          title="تعديل مخزن"
          actions={
            <Link href="/inventory/warehouses">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'المخزن غير موجود.'}
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل مخزن"
        description={`تعديل بيانات: ${warehouse.name}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخازن', href: '/inventory/warehouses' },
          { label: warehouse.name, href: `/inventory/warehouses/${warehouse.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href={`/inventory/warehouses/${warehouse.id}`}>
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
          <div className="grid grid-cols-2 gap-4">
            <div>
              <p className="text-xs text-gray-500 mb-1">الكود (غير قابل للتعديل)</p>
              <p className="font-mono text-sm text-gray-700 bg-gray-50 px-3 py-2 rounded border border-gray-200">
                {warehouse.code}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الشركة (غير قابلة للتعديل)</p>
              <p className="font-mono text-xs text-gray-700 bg-gray-50 px-3 py-2 rounded border border-gray-200" dir="ltr">
                {warehouse.companyId.substring(0, 8)}…
              </p>
            </div>
          </div>

          <Input
            label="اسم المخزن *"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
          />

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
            placeholder="اتركه فارغاً لإزالة المدير"
          />

          <label className="flex items-center gap-2 text-sm text-gray-700">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(e) => setIsActive(e.target.checked)}
              className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
            />
            <span>نشط</span>
          </label>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ
            </Button>
            <Link href={`/inventory/warehouses/${warehouse.id}`}>
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
