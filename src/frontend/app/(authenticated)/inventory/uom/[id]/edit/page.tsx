'use client';

// صفحة تعديل وحدة قياس (UoM) — v1.0.10: supports PUT/DELETE

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Trash2 } from 'lucide-react';
import { Button, Card, PageHeader, Input, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { uomApi, getErrorMessage } from '@/lib/api';

interface UoM {
  id: string;
  code: string;
  name: string;
  symbol?: string;
  isActive: boolean;
}

export default function EditUoMPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<UoM | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // form state
  const [name, setName] = useState('');
  const [symbol, setSymbol] = useState('');

  useEffect(() => {
    const load = async () => {
      try {
        const data = await uomApi.get(params.id);
        setItem(data);
        setName(data.name);
        setSymbol(data.symbol || '');
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل التحميل'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!item) return;
    setError(null);
    setSubmitting(true);
    try {
      await uomApi.update(item.id, { code: item.code, name, symbol: symbol || undefined });
      router.push('/inventory/uom');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل حفظ التعديلات.'));
      setSubmitting(false);
    }
  };

  const handleDeactivate = async () => {
    if (!item) return;
    if (!confirm('هل أنت متأكد من إلغاء تفعيل وحدة القياس هذه؟')) return;
    setSubmitting(true);
    try {
      await uomApi.deactivate(item.id);
      router.push('/inventory/uom');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="وحدة قياس" />
        <Card className="max-w-xl"><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card>
      </div>
    );
  }

  if (!item) {
    return (
      <div>
        <PageHeader title="وحدة قياس" />
        <Card className="max-w-xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'وحدة القياس غير موجودة'}</div>
          <div className="mt-4"><Link href="/inventory/uom"><Button variant="ghost">رجوع</Button></Link></div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="📏 تعديل وحدة قياس"
        description={`${item.code} (الكود ثابت لتجنب كسر الـ FKs في items.unit_of_measure_id)`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'المخزون', href: '/inventory' },
          { label: 'وحدات القياس', href: '/inventory/uom' },
          { label: item.code },
        ]}
        actions={
          <Link href="/inventory/uom">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">الكود</label>
              <div className="font-mono text-blue-600 text-lg bg-gray-50 px-3 py-2 rounded-lg border">{item.code}</div>
            </div>
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">الحالة</label>
              <div className="pt-1"><Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'فعّال' : 'غير فعّال'}</Badge></div>
            </div>
          </div>

          <Input
            label="الاسم *"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="مثال: كيلوغرام"
          />

          <Input
            label="الرمز (اختياري)"
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="مثال: kg"
          />

          <div className="flex items-center justify-between gap-2 pt-3 border-t">
            <div className="flex items-center gap-2">
              <Button
                type="submit"
                variant="primary"
                loading={submitting}
                iconLeft={<Save className="h-4 w-4" />}
              >
                حفظ التعديلات
              </Button>
              <Link href="/inventory/uom">
                <Button type="button" variant="ghost">إلغاء</Button>
              </Link>
            </div>
            {item.isActive && (
              <Button
                type="button"
                variant="danger"
                onClick={handleDeactivate}
                disabled={submitting}
                iconLeft={<Trash2 className="h-4 w-4" />}
              >
                إلغاء التفعيل
              </Button>
            )}
          </div>
        </form>
      </Card>
    </div>
  );
}
