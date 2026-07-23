'use client';

// تفاصيل مخزن (Warehouse Detail)

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Pencil, Power, Hash, MapPin, Warehouse as WarehouseIcon, CheckCircle2, XCircle, User } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { warehousesApi, Warehouse, getErrorMessage } from '@/lib/api';

export default function WarehouseDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [warehouse, setWarehouse] = useState<Warehouse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const w = await warehousesApi.get(params.id);
      setWarehouse(w);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل المخزن.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!warehouse) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف المخزن "${warehouse.name}"؟`)) return;
    setDeactivating(true);
    try {
      await warehousesApi.deactivate(warehouse.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف المخزن.'));
    } finally {
      setDeactivating(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="مخزن" />
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
          title="مخزن"
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
          <div className="mt-4">
            <Link href="/inventory/warehouses">
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
                warehouse.isActive
                  ? 'bg-amber-100 text-amber-700'
                  : 'bg-gray-100 text-gray-500'
              }`}
            >
              <WarehouseIcon className="h-5 w-5" />
            </div>
            <div>
              <span>{warehouse.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {warehouse.code}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {warehouse.isActive ? (
              <Badge variant="success">نشط</Badge>
            ) : (
              <Badge variant="warning">معطّل</Badge>
            )}
            {warehouse.location && (
              <span className="inline-flex items-center gap-1 text-xs text-gray-500">
                <MapPin className="h-3 w-3" /> {warehouse.location}
              </span>
            )}
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخازن', href: '/inventory/warehouses' },
          { label: warehouse.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/inventory/warehouses">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            {warehouse.isActive && (
              <Link href={`/inventory/warehouses/${warehouse.id}/edit`}>
                <Button variant="primary" iconLeft={<Pencil className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}
            {warehouse.isActive && (
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
        <Card title="📋 المعلومات الأساسية" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Hash className="h-4 w-4" /> الكود
              </p>
              <p className="font-mono">{warehouse.code}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <WarehouseIcon className="h-4 w-4" /> الاسم
              </p>
              <p>{warehouse.name}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <MapPin className="h-4 w-4" /> الموقع
              </p>
              <p>{warehouse.location || '— غير محدد —'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <User className="h-4 w-4" /> مدير المخزن
              </p>
              <p className="font-mono text-xs" dir="ltr">
                {warehouse.managerUserId
                  ? `${warehouse.managerUserId.substring(0, 8)}…`
                  : '— غير معيَّن —'}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {warehouse.isActive ? (
                <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                  <CheckCircle2 className="h-4 w-4" /> نشط
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                  <XCircle className="h-4 w-4" /> معطّل
                </span>
              )}
            </div>
          </div>
        </Card>

        <Card title="🔍 معلومات تقنية">
          <div className="space-y-3 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1">المعرّف (ID)</p>
              <p className="text-xs font-mono text-gray-700 break-all" dir="ltr">
                {warehouse.id}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Tenant</p>
              <p className="text-xs font-mono text-gray-700 break-all" dir="ltr">
                {warehouse.tenantId}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الشركة (Company)</p>
              <p className="text-xs font-mono text-gray-700 break-all" dir="ltr">
                {warehouse.companyId}
              </p>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
