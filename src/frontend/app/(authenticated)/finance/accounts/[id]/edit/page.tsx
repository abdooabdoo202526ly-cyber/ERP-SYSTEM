'use client';

// صفحة تعديل حساب (Account) — v1.0.10: تدعم التعديل الكامل (PUT)

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Trash2 } from 'lucide-react';
import { Button, Card, PageHeader, Input, Select, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { financeApi, getErrorMessage } from '@/lib/api';

interface Account {
  id: string;
  tenantId: string;
  code: string;
  name: string;
  description?: string;
  type: number;
  normalBalance: number;
  parentAccountId?: string;
  isPostable: boolean;
  isActive: boolean;
}

const ACCOUNT_TYPES: { value: number; label: string }[] = [
  { value: 1, label: 'أصول' },
  { value: 2, label: 'خصوم' },
  { value: 3, label: 'حقوق ملكية' },
  { value: 4, label: 'إيرادات' },
  { value: 5, label: 'مصروفات' },
];

export default function EditAccountPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<Account | null>(null);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // form state
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [type, setType] = useState<number>(1);
  const [isPostable, setIsPostable] = useState(true);
  const [isActive, setIsActive] = useState(true);

  useEffect(() => {
    const load = async () => {
      try {
        // v1.0.21: use financeApi (axios) to get error handling
        const data = await financeApi.getAccount(params.id);
        setItem(data);
        setName(data.name);
        setDescription(data.description || '');
        setType(data.type);
        setIsPostable(data.isPostable);
        setIsActive(data.isActive);
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
      // v1.0.24: include type and normalBalance in update so edits actually persist
      await financeApi.updateAccount(item.id, {
        name,
        description: description || undefined,
        type,
        normalBalance: item.normalBalance,
        isPostable,
        isActive,
      } as Partial<Account>);
      router.push('/finance/accounts');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل حفظ التعديلات.'));
      setSubmitting(false);
    }
  };

  const handleDeactivate = async () => {
    if (!item) return;
    if (!confirm('هل أنت متأكد من إلغاء تفعيل هذا الحساب؟')) return;
    setSubmitting(true);
    try {
      await financeApi.deleteAccount(item.id);
      router.push('/finance/accounts');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
      setSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="حساب" />
        <Card className="max-w-2xl"><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card>
      </div>
    );
  }

  if (!item) {
    return (
      <div>
        <PageHeader title="حساب" />
        <Card className="max-w-2xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'الحساب غير موجود'}</div>
          <div className="mt-4"><Link href="/finance/accounts"><Button variant="ghost">رجوع</Button></Link></div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title="💰 تعديل حساب"
        description={`${item.code} (الكود ثابت ولا يمكن تغييره لأسباب محاسبية)`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'دليل الحسابات', href: '/finance/accounts' },
          { label: item.code, href: `/finance/accounts/${item.id}` },
          { label: 'تعديل' },
        ]}
        actions={
          <Link href="/finance/accounts">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">الكود</label>
              <div className="font-mono text-blue-600 text-lg bg-gray-50 px-3 py-2 rounded-lg border">{item.code}</div>
            </div>
            <Select
              label="النوع"
              value={String(type)}
              onChange={(e) => setType(Number(e.target.value))}
              options={ACCOUNT_TYPES.map((t) => ({ value: String(t.value), label: t.label }))}
            />
          </div>

          <Input
            label="اسم الحساب *"
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="مثال: النقدية في الصندوق"
          />

          <Input
            label="الوصف"
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            placeholder="اختياري"
          />

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Select
              label="قابل للترحيل"
              value={isPostable ? '1' : '0'}
              onChange={(e) => setIsPostable(e.target.value === '1')}
              options={[
                { value: '1', label: 'نعم (يستقبل قيود)' },
                { value: '0', label: 'لا (حساب رئيسي فقط)' },
              ]}
            />
            <Select
              label="الحالة"
              value={isActive ? '1' : '0'}
              onChange={(e) => setIsActive(e.target.value === '1')}
              options={[
                { value: '1', label: 'فعّال' },
                { value: '0', label: 'غير فعّال' },
              ]}
            />
          </div>

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
              <Link href="/finance/accounts">
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
