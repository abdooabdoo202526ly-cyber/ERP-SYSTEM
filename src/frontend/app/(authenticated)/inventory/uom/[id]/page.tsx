'use client';

// تفاصيل وحدة قياس (Unit of Measure Detail)
// الـ Backend لا يدعم Update/Deactivate لوحدة القياس (Create + List + GetById فقط) —
// لذا هذه الصفحة read-only. في حال الإضافة لاحقاً، نضيف زرّ تعديل.

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Hash, Type, Sigma, Ruler, CheckCircle2, XCircle } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { uomApi, UnitOfMeasure, getErrorMessage } from '@/lib/api';

export default function UomDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [uom, setUom] = useState<UnitOfMeasure | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const u = await uomApi.get(params.id);
      setUom(u);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل وحدة القياس.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  if (loading) {
    return (
      <div>
        <PageHeader title="وحدة قياس" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!uom) {
    return (
      <div>
        <PageHeader
          title="وحدة قياس"
          actions={
            <Link href="/inventory/uom">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'وحدة القياس غير موجودة.'}
          </div>
          <div className="mt-4">
            <Link href="/inventory/uom">
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
                uom.isActive
                  ? 'bg-emerald-100 text-emerald-700'
                  : 'bg-gray-100 text-gray-500'
              }`}
            >
              <Ruler className="h-5 w-5" />
            </div>
            <div>
              <span>{uom.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {uom.code}
                {uom.symbol ? ` (${uom.symbol})` : ''}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            {uom.isActive ? (
              <Badge variant="success">نشط</Badge>
            ) : (
              <Badge variant="warning">معطّل</Badge>
            )}
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'وحدات القياس', href: '/inventory/uom' },
          { label: uom.name },
        ]}
        actions={
          <Link href="/inventory/uom">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
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
              <p className="font-mono">{uom.code}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Type className="h-4 w-4" /> الاسم
              </p>
              <p>{uom.name}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                <Sigma className="h-4 w-4" /> الرمز
              </p>
              <p>{uom.symbol || '— غير محدد —'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {uom.isActive ? (
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

          <div className="mt-4 pt-4 border-t">
            <p className="text-xs text-gray-500">
              💡 <span className="font-semibold">ملاحظة:</span> وحدات القياس لا تُعدَّل بعد
              إنشائها للحفاظ على تكامل المخزون والـ conversions. أضف وحدة جديدة بدلاً من ذلك.
            </p>
          </div>
        </Card>

        <Card title="🔍 معلومات تقنية">
          <div className="space-y-3 text-sm">
            <div>
              <p className="text-xs text-gray-500 mb-1">المعرّف (ID)</p>
              <p className="text-xs font-mono text-gray-700 break-all" dir="ltr">
                {uom.id}
              </p>
            </div>
          </div>
        </Card>
      </div>
    </div>
  );
}
