'use client';

// تفاصيل قسم (Department Detail) — عرض + زر تعديل + زر إيقاف

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Pencil, Power, Hash, Building2, CheckCircle2, XCircle } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { departmentsApi, Department, getErrorMessage } from '@/lib/api';

function formatDate(s?: string): string {
  if (!s) return '-';
  try {
    return new Date(s).toLocaleString('ar-LY', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return s;
  }
}

export default function DepartmentDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [department, setDepartment] = useState<Department | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const d = await departmentsApi.get(params.id);
      setDepartment(d);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل القسم.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!department) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف القسم "${department.name}"؟`)) return;
    setDeactivating(true);
    try {
      await departmentsApi.deactivate(department.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف القسم.'));
    } finally {
      setDeactivating(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="قسم" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!department) {
    return (
      <div>
        <PageHeader
          title="قسم"
          actions={
            <Link href="/hr/departments">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'القسم غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/hr/departments">
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
                department.isActive
                  ? 'bg-blue-100 text-blue-700'
                  : 'bg-gray-100 text-gray-500'
              }`}
            >
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <span>{department.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {department.code}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {department.isActive ? (
              <Badge variant="success">نشط</Badge>
            ) : (
              <Badge variant="warning">معطّل</Badge>
            )}
            {department.parentId ? (
              <Badge variant="info">قسم فرعي</Badge>
            ) : (
              <Badge variant="neutral">قسم جذر</Badge>
            )}
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الأقسام', href: '/hr/departments' },
          { label: department.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/hr/departments">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            {department.isActive && (
              <Link href={`/hr/departments/${department.id}/edit`}>
                <Button variant="primary" iconLeft={<Pencil className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}
            {department.isActive && (
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
              <p className="font-mono">{department.code}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Building2 className="h-4 w-4" /> الاسم
              </p>
              <p>{department.name}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">القسم الأب</p>
              <p className="font-mono text-xs text-gray-700" dir="ltr">
                {department.parentId ? `${department.parentId.substring(0, 8)}…` : '— جذر —'}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">مدير القسم</p>
              <p className="font-mono text-xs text-gray-700" dir="ltr">
                {department.managerId ? `${department.managerId.substring(0, 8)}…` : '— غير معيَّن —'}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {department.isActive ? (
                <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                  <CheckCircle2 className="h-4 w-4" /> نشط
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                  <XCircle className="h-4 w-4" /> معطّل
                </span>
              )}
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">تاريخ آخر تحديث</p>
              <p>{formatDate(department.updatedAt)}</p>
            </div>
          </div>
        </Card>

        <Card title="🔍 معلومات تقنية">
          <div className="space-y-3 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1">المعرّف (ID)</p>
              <p className="text-xs font-mono text-gray-700" dir="ltr">
                {department.id}
              </p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">Tenant</p>
              <p className="text-xs font-mono text-gray-700" dir="ltr">
                {department.tenantId}
              </p>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
