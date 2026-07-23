'use client';

// قائمة قواعد الترحيل (Posting Rules)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye, Pencil } from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface PostingRule {
  id: string;
  name: string;
  description?: string;
  eventType: number;
  isActive: boolean;
  templateJson: string;
  createdAt: string;
}

const EVENT_LABELS: Record<number, string> = {
  1: 'استلام مخزون (StockReceived)',
  2: 'صرف مخزون (StockIssued)',
  3: 'إنشاء فاتورة (InvoiceCreated)',
  4: 'استلام دفعة (PaymentReceived)',
};

export default function PostingRulesPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<PostingRule[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filter, setFilter] = useState('');

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/finance/posting-rules', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = items.filter(
    (r) => !filter || r.name.includes(filter) || (r.description && r.description.includes(filter))
  );

  return (
    <div>
      <PageHeader
        title="⚙️ قواعد الترحيل"
        description="قواعد ربط أحداث النظام بقيود محاسبية"
        actions={
          <div className="flex items-center gap-2">
            <Link href="/admin/posting-rules/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>قاعدة جديدة</Button>
            </Link>
            <Input placeholder="🔍 بحث..." value={filter} onChange={(e) => setFilter(e.target.value)} containerClassName="w-64" />
          </div>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">لا توجد قواعد ترحيل.</div>
      ) : (
        <div className="space-y-2">
          {filtered.map((r) => (
            <Card key={r.id} accent={r.isActive ? 'green' : 'gray'}>
              <div className="flex items-start justify-between">
                <div>
                  <div className="flex items-center gap-2">
                    <h3 className="font-bold text-gray-800">{r.name}</h3>
                    <Badge variant={r.isActive ? 'success' : 'neutral'}>{r.isActive ? 'فعّال' : 'معطّل'}</Badge>
                  </div>
                  {r.description && <p className="text-sm text-gray-500 mt-1">{r.description}</p>}
                  <div className="mt-2 flex items-center gap-2 text-xs">
                    <span className="text-gray-500">الحدث:</span>
                    <Badge variant="info">{EVENT_LABELS[r.eventType] || `Event ${r.eventType}`}</Badge>
                  </div>
                </div>
                <div className="flex items-center gap-1">
                  <Link href={`/admin/posting-rules/${r.id}`}>
                    <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3.5 w-3.5" />}>
                      عرض
                    </Button>
                  </Link>
                  <Link href={`/admin/posting-rules/${r.id}/edit`}>
                    <Button variant="ghost" size="sm" iconLeft={<Pencil className="h-3.5 w-3.5" />}>
                      تعديل
                    </Button>
                  </Link>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
