'use client';

// إنشاء وحدة قياس جديدة (Unit of Measure)

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Hash, Type, Sigma } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { uomApi, getErrorMessage } from '@/lib/api';

export default function NewUomPage() {
  const router = useRouter();
  useAuth();
  const [code, setCode] = useState('');
  const [name, setName] = useState('');
  const [symbol, setSymbol] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      const created = await uomApi.create({
        code: code.trim(),
        name: name.trim(),
        symbol: symbol.trim() || undefined,
      });
      router.push(`/inventory/uom/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء وحدة القياس.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ وحدة قياس جديدة"
        description="إنشاء وحدة قياس جديدة (Unit of Measure)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'وحدات القياس', href: '/inventory/uom' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/inventory/uom">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <Card className="max-w-xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
            {error}
          </div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <Input
            label={
              <span className="inline-flex items-center gap-1">
                <Hash className="h-3 w-3" /> الكود *
              </span>
            }
            value={code}
            onChange={(e) => setCode(e.target.value)}
            required
            placeholder="pcs, kg, m, l"
          />
          <Input
            label={
              <span className="inline-flex items-center gap-1">
                <Type className="h-3 w-3" /> الاسم *
              </span>
            }
            value={name}
            onChange={(e) => setName(e.target.value)}
            required
            placeholder="مثال: قطعة، كيلوجرام، متر، لتر"
          />
          <Input
            label={
              <span className="inline-flex items-center gap-1">
                <Sigma className="h-3 w-3" /> الرمز (اختياري)
              </span>
            }
            value={symbol}
            onChange={(e) => setSymbol(e.target.value)}
            placeholder="مثال: kg, m, L"
          />

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ
            </Button>
            <Link href="/inventory/uom">
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
