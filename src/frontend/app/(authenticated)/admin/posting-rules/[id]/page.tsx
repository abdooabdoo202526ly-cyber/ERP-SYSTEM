'use client';

// تفاصيل قاعدة ترحيل (Posting Rule Detail)

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
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

export default function PostingRuleDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<PostingRule | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const load = async () => {
      try {
        const res = await fetch('/api/finance/posting-rules', { cache: 'no-store' });
        if (!res.ok) throw new Error('فشل التحميل');
        const list = await res.json();
        const found = list.find((x: PostingRule) => x.id === params.id);
        if (!found) throw new Error('القاعدة غير موجودة');
        setItem(found);
      } catch (e: unknown) {
        setError(getErrorMessage(e, 'فشل التحميل'));
      } finally {
        setLoading(false);
      }
    };
    load();
  }, [params.id]);

  if (loading) return <div><PageHeader title="قاعدة" /><Card><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card></div>;
  if (!item) return <div><PageHeader title="قاعدة" /><Card><div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'غير موجود'}</div><div className="mt-4"><Link href="/admin/posting-rules"><Button variant="ghost">رجوع</Button></Link></div></Card></div>;

  let parsedTemplate: { description?: string; reference?: string; lines?: { accountCode: string; side: string; amountFormula: string }[] } = {};
  try {
    parsedTemplate = JSON.parse(item.templateJson);
  } catch {
    parsedTemplate = {};
  }

  return (
    <div>
      <PageHeader
        title="⚙️ قاعدة ترحيل"
        description={item.name}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'قواعد الترحيل', href: '/admin/posting-rules' },
          { label: item.name.substring(0, 30) },
        ]}
        actions={
          <Link href="/admin/posting-rules">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

      <Card className="max-w-3xl">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={item.isActive ? 'success' : 'neutral'}>{item.isActive ? 'فعّال' : 'معطّل'}</Badge>
          </div>
          <div>
            <p className="text-gray-500">نوع الحدث</p>
            <Badge variant="info">{EVENT_LABELS[item.eventType] || `Event ${item.eventType}`}</Badge>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الوصف</p>
            <p>{item.description || '-'}</p>
          </div>
        </div>
      </Card>

      <Card className="mt-4 max-w-3xl">
        <h3 className="font-bold text-gray-800 mb-3">القالب (Template)</h3>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">وصف القيد</p>
            <p>{parsedTemplate.description || '-'}</p>
          </div>
          <div>
            <p className="text-gray-500">المرجع</p>
            <p className="font-mono">{parsedTemplate.reference || '-'}</p>
          </div>
        </div>
        <h4 className="font-bold text-sm text-gray-700 mt-4 mb-2">السطور ({parsedTemplate.lines?.length || 0}):</h4>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-gray-600">
                <th className="py-2 px-2 text-start">كود الحساب</th>
                <th className="py-2 px-2 text-start">الجانب</th>
                <th className="py-2 px-2 text-start">صيغة المبلغ</th>
              </tr>
            </thead>
            <tbody>
              {(parsedTemplate.lines || []).map((line, idx) => (
                <tr key={idx} className="border-b">
                  <td className="py-2 px-2 font-mono text-blue-600">{line.accountCode}</td>
                  <td className="py-2 px-2">
                    <Badge variant={line.side === 'debit' ? 'info' : 'warning'}>{line.side === 'debit' ? 'مدين' : 'دائن'}</Badge>
                  </td>
                  <td className="py-2 px-2 font-mono">{line.amountFormula}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <details className="mt-4">
          <summary className="cursor-pointer text-xs text-gray-500">عرض JSON الخام</summary>
          <pre className="mt-2 p-3 bg-gray-50 rounded text-xs overflow-x-auto font-mono">{item.templateJson}</pre>
        </details>
      </Card>
    </div>
  );
}
