'use client';

// تفاصيل قيد محاسبي (Journal Entry Detail)

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Send } from 'lucide-react';
import { Button, Card, PageHeader, Badge } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface JournalLine {
  lineNumber: number;
  accountId: string;
  accountCode: string;
  accountName: string;
  debit: number;
  credit: number;
  description?: string;
}

interface JournalEntry {
  id: string;
  entryNumber: string;
  entryDate: string;
  description: string;
  reference?: string;
  status: number;
  postedAt?: string;
  lines: JournalLine[];
  totalDebit: number;
  totalCredit: number;
}

const STATUS_BADGE: Record<number, { label: string; variant: 'neutral' | 'success' | 'warning' }> = {
  1: { label: 'مسودة (Draft)', variant: 'warning' },
  2: { label: 'مُرحَّل (Posted)', variant: 'success' },
  3: { label: 'معكوس (Reversed)', variant: 'neutral' },
};

export default function JournalEntryDetailPage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  useAuth();
  const [entry, setEntry] = useState<JournalEntry | null>(null);
  const [loading, setLoading] = useState(true);
  const [posting, setPosting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      const res = await fetch(`/api/finance/journal-entries/${params.id}`, { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setEntry(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [params.id]);

  const handlePost = async () => {
    if (!entry) return;
    if (!confirm('هل تريد ترحيل القيد؟ لن تتمكن من تعديله بعد الترحيل.')) return;
    setPosting(true);
    try {
      const res = await fetch(`/api/finance/journal-entries/${entry.id}/post`, { method: 'POST' });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل الترحيل');
      }
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل ترحيل القيد.'));
    } finally {
      setPosting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="قيد محاسبي" />
        <Card className="max-w-4xl"><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card>
      </div>
    );
  }

  if (!entry) {
    return (
      <div>
        <PageHeader title="قيد محاسبي" />
        <Card className="max-w-4xl">
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'القيد غير موجود'}</div>
          <div className="mt-4"><Link href="/finance/journal-entries"><Button variant="ghost">رجوع</Button></Link></div>
        </Card>
      </div>
    );
  }

  const statusInfo = STATUS_BADGE[entry.status] || { label: `Status ${entry.status}`, variant: 'neutral' as const };
  const balanced = Math.abs(Number(entry.totalDebit) - Number(entry.totalCredit)) < 0.01;

  return (
    <div>
      <PageHeader
        title="📒 قيد محاسبي"
        description={entry.entryNumber}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'القيود', href: '/finance/journal-entries' },
          { label: entry.entryNumber },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {entry.status === 1 && (
              <Button variant="primary" onClick={handlePost} loading={posting} iconLeft={<Send className="h-4 w-4" />}>
                ترحيل القيد
              </Button>
            )}
            <Link href="/finance/journal-entries">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Card className="max-w-4xl">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 text-sm">
          <div>
            <p className="text-gray-500">رقم القيد</p>
            <p className="font-mono text-blue-600">{entry.entryNumber}</p>
          </div>
          <div>
            <p className="text-gray-500">التاريخ</p>
            <p className="font-mono">{new Date(entry.entryDate).toLocaleDateString('en-GB')}</p>
          </div>
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={statusInfo.variant}>{statusInfo.label}</Badge>
          </div>
          <div>
            <p className="text-gray-500">التوازن</p>
            <Badge variant={balanced ? 'success' : 'danger'}>{balanced ? '✅ متوازن' : '❌ غير متوازن'}</Badge>
          </div>
          {entry.reference && (
            <div className="col-span-2">
              <p className="text-gray-500">المرجع</p>
              <p className="font-mono text-gray-800">{entry.reference}</p>
            </div>
          )}
          {entry.postedAt && (
            <div className="col-span-2">
              <p className="text-gray-500">تاريخ الترحيل</p>
              <p className="font-mono text-gray-800">{new Date(entry.postedAt).toLocaleString('en-GB')}</p>
            </div>
          )}
          <div className="col-span-4">
            <p className="text-gray-500">الوصف</p>
            <p className="font-bold text-gray-800">{entry.description}</p>
          </div>
        </div>
      </Card>

      {/* Lines table */}
      <Card className="mt-4 max-w-4xl">
        <h3 className="font-bold text-gray-800 mb-3">البنود ({entry.lines.length})</h3>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b text-start text-gray-600">
                <th className="py-2 px-2 text-start">#</th>
                <th className="py-2 px-2 text-start">الحساب</th>
                <th className="py-2 px-2 text-start">الوصف</th>
                <th className="py-2 px-2 text-end">مدين</th>
                <th className="py-2 px-2 text-end">دائن</th>
              </tr>
            </thead>
            <tbody>
              {entry.lines.map((l) => (
                <tr key={l.lineNumber} className="border-b">
                  <td className="py-2 px-2 font-mono text-gray-500">{l.lineNumber}</td>
                  <td className="py-2 px-2">
                    <span className="font-mono text-blue-600">{l.accountCode}</span>
                    <span className="text-gray-800 ms-2">{l.accountName}</span>
                  </td>
                  <td className="py-2 px-2 text-gray-600">{l.description || '-'}</td>
                  <td className="py-2 px-2 text-end font-mono">{(Number(l.debit) || 0).toFixed(2)}</td>
                  <td className="py-2 px-2 text-end font-mono">{(Number(l.credit) || 0).toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
            <tfoot>
              <tr className="font-bold border-t-2">
                <td colSpan={3} className="py-2 px-2 text-end">الإجمالي:</td>
                <td className="py-2 px-2 text-end font-mono text-blue-600">{(Number(entry.totalDebit) || 0).toFixed(2)}</td>
                <td className="py-2 px-2 text-end font-mono text-orange-600">{(Number(entry.totalCredit) || 0).toFixed(2)}</td>
              </tr>
            </tfoot>
          </table>
        </div>
      </Card>

      {entry.status === 1 && (
        <p className="max-w-4xl mt-3 text-xs text-gray-500">
          💡 القيد في حالة مسودة. اضغط زر ترحيل القيد لنشره في دفتر الأستاذ. لا يمكن تعديل بنوده بعد الترحيل.
        </p>
      )}
      {entry.status === 2 && (
        <p className="max-w-4xl mt-3 text-xs text-gray-500">
          💡 القيد مُرحَّل. التغييرات تتطلب قيد عكسي.
        </p>
      )}
    </div>
  );
}
