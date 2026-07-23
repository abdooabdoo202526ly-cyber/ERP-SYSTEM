'use client';

// إنشاء فئة أصناف جديدة (Item Category)

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface CategoryOption {
  id: string;
  code: string;
  name: string;
}

interface FormState {
  code: string;
  name: string;
  description: string;
  parentId: string;
}

export default function NewCategoryPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({ code: '', name: '', description: '', parentId: '' });
  const [parents, setParents] = useState<CategoryOption[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch('/api/inventory/categories');
        if (!res.ok) return;
        const data = await res.json();
        setParents(data.map((c: CategoryOption) => ({ id: c.id, code: c.code, name: c.name })));
      } catch {
        // ignore
      }
    };
    load();
  }, []);

  const onChange = <K extends keyof FormState>(k: K) => (
    e: React.ChangeEvent<HTMLInputElement | HTMLSelectElement>
  ) => setForm((f) => ({ ...f, [k]: e.target.value }));

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/api/inventory/categories', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          ...form,
          parentId: form.parentId || null,
          isActive: true,
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء الفئة');
      }
      router.push('/admin/item-categories');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء الفئة.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ فئة جديدة"
        description="أضف تصنيفاً جديداً للأصناف"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الفئات', href: '/admin/item-categories' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/admin/item-categories">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-2xl">
        {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <Input label="الكود *" value={form.code} onChange={onChange('code')} required placeholder="مثال: CAT-001" />
            <Input label="الاسم *" value={form.name} onChange={onChange('name')} required placeholder="مثال: مواد بناء" />
          </div>
          <Input label="الوصف" value={form.description} onChange={onChange('description')} placeholder="اختياري" />

          <div>
            <label className="block text-sm text-gray-500 mb-1">الفئة الأب (للهيكلة الهرمية)</label>
            <select
              value={form.parentId}
              onChange={onChange('parentId')}
              className="w-full border border-gray-300 rounded px-3 py-2 text-sm"
            >
              <option value="">— لا يوجد (فئة جذر) —</option>
              {parents.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} - {p.name}
                </option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} iconLeft={<Save className="h-4 w-4" />}>حفظ</Button>
            <Link href="/admin/item-categories">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
