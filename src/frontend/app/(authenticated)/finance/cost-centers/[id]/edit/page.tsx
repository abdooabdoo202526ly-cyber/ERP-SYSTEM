'use client';

// عرض تفاصيل مركز التكلفة (Cost Center)
// ملاحظة: Backend لا يدعم PUT لتعديل مراكز التكلفة،
// هذه الصفحة تعرض التفاصيل فقط مع خيار إلغاء التفعيل

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Trash2 } from 'lucide-react';
import { Button, Card, PageHeader, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface CostCenter {
  id: string;
  tenantId: string;
  companyId: string;
  code: string;
  name: string;
  description?: string;
  type: number;
  parentId?: string;
  isActive: boolean;
  createdAt: string;
}

const CC_TYPES: Record<number, string> = {
  1: 'إنتاج',
  2: 'خدمات',
  3: 'إداري',
  4: 'مبيعات',
};

export default function EditCostCenterPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<CostCenter | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionRunning, setActionRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch(`/api/cost-centers/${params.id}`);
        if (!res.ok) throw new Error('فشل التحميل');
        const data = await res.json();
        setItem(data);
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل التحميل'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  const handleDeactivate = async () => {
    if (!item) return;
    if (!confirm('هل أنت متأكد من إلغاء تفعيل هذا المركز؟')) return;
    setActionRunning(true);
    try {
      const res = await fetch(`/api/cost-centers/${item.id}`, { method: 'DELETE' });
      if (!res.ok && res.status !== 204) {
        const t = await res.text();
        throw new Error(t || 'فشل الإلغاء');
      }
      router.push('/finance/cost-centers');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
      setActionRunning(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="مركز التكلفة" />
        <Card className="max-w-2xl"><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card>
      </div>
    );
  }

  if (!item) {
    return (
      <div>
        <PageHeader title="مركز التكلفة" />
        <Card className="max-w-2xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'المركز غير موجود'}
          </div>
          <div className="mt-4">
            <Link href="/finance/cost-centers"><Button variant="ghost">رجوع</Button></Link>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="🏢 مركز تكلفة"
        description={item.name}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'مراكز التكلفة', href: '/finance/cost-centers' },
          { label: item.code },
        ]}
        actions={
          <Link href="/finance/cost-centers">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">الكود</p>
            <p className="font-mono text-gray-800">{item.code}</p>
          </div>
          <div>
            <p className="text-gray-500">النوع</p>
            <Badge variant="info">{CC_TYPES[item.type] || item.type}</Badge>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الاسم</p>
            <p className="font-bold text-gray-800">{item.name}</p>
          </div>
          {item.description && (
            <div className="col-span-2">
              <p className="text-gray-500">الوصف</p>
              <p className="text-gray-800">{item.description}</p>
            </div>
          )}
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={item.isActive ? 'success' : 'neutral'}>
              {item.isActive ? 'فعّال' : 'غير فعّال'}
            </Badge>
          </div>
          <div>
            <p className="text-gray-500">تاريخ الإنشاء</p>
            <p className="font-mono text-gray-800">{new Date(item.createdAt).toLocaleDateString('en-GB')}</p>
          </div>
        </div>

        <div className="mt-6 pt-4 border-t">
          <p className="text-xs text-gray-500 mb-3">
            💡 ملاحظة: لا يدعم النظام تعديل مراكز التكلفة بعد الإنشاء (لأسباب محاسبية).
            يمكنك إلغاء التفعيل وإنشاء مركز جديد بدلاً من ذلك.
          </p>
          {item.isActive && (
            <Button
              variant="danger"
              onClick={handleDeactivate}
              loading={actionRunning}
              iconLeft={<Trash2 className="h-4 w-4" />}
            >
              إلغاء التفعيل
            </Button>
          )}
        </div>
      </Card>
    </div>
  );
}
