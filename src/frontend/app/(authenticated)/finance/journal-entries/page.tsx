'use client';

// قائمة القيود المحاسبية (Journal Entries)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Eye } from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface JournalEntry {
  id: string;
  entryNumber: string;
  entryDate: string;
  description: string;
  reference?: string;
  status: number; // 1=Draft, 2=Posted, 3=Reversed
  postedAt?: string;
  totalDebit: number;
  totalCredit: number;
}

const JE_STATUSES: Record<number, { label: string; variant: 'neutral' | 'success' | 'warning' }> = {
  1: { label: 'مسودة (Draft)', variant: 'warning' },
  2: { label: 'مُرحَّل (Posted)', variant: 'success' },
  3: { label: 'معكوس (Reversed)', variant: 'neutral' },
};

export default function JournalEntriesPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<JournalEntry[]>([]);
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
      const res = await fetch('/api/finance/journal-entries', { cache: 'no-store' });
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
    (j) => !filter || j.entryNumber?.includes(filter) || j.description?.includes(filter)
  );

  const totalDr = items.reduce((s, j) => s + Number(j.totalDebit || 0), 0);
  const totalCr = items.reduce((s, j) => s + Number(j.totalCredit || 0), 0);
  const balanced = Math.abs(totalDr - totalCr) < 0.01;

  return (
    <div>
      <PageHeader
        title="📒 القيود المحاسبية"
        description="القيود اليومية ودفتر الأستاذ"
        actions={
          <div className="flex items-center gap-2">
            <Link href="/finance/journal-entries/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>قيد جديد</Button>
            </Link>
            <Input
              placeholder="🔍 رقم/وصف..."
              value={filter}
              onChange={(e) => setFilter(e.target.value)}
              containerClassName="w-64"
            />
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>
      )}

      {/* Summary card */}
      {!loading && items.length > 0 && (
        <Card className="mb-4" accent={balanced ? 'green' : 'red'}>
          <div className="grid grid-cols-3 gap-4 text-sm">
            <div>
              <p className="text-gray-500">إجمالي المدين</p>
              <p className="font-bold text-xl text-gray-800">{totalDr.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
            </div>
            <div>
              <p className="text-gray-500">إجمالي الدائن</p>
              <p className="font-bold text-xl text-gray-800">{totalCr.toLocaleString(undefined, { minimumFractionDigits: 2 })}</p>
            </div>
            <div>
              <p className="text-gray-500">التوازن</p>
              <Badge variant={balanced ? 'success' : 'danger'}>
                {balanced ? '✅ متوازن' : '❌ غير متوازن'}
              </Badge>
            </div>
          </div>
        </Card>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : filtered.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          لا توجد قيود محاسبية.
        </div>
      ) : (
        <div className="space-y-3">
          {filtered.map((j) => {
            const statusInfo = JE_STATUSES[j.status] || { label: `Status ${j.status}`, variant: 'neutral' as const };
            const jeBalanced = Math.abs(Number(j.totalDebit) - Number(j.totalCredit)) < 0.01;
            return (
              <Card key={j.id} accent="purple">
                <div className="flex items-start justify-between">
                  <div>
                    <p className="text-xs text-gray-500 font-mono">{j.entryNumber}</p>
                    <h3 className="font-bold text-gray-800 mt-1">{j.description}</h3>
                    <div className="flex items-center gap-3 mt-2 text-xs text-gray-500">
                      <span>📅 {new Date(j.entryDate).toLocaleDateString('en-GB')}</span>
                      {j.reference && <span>🔖 {j.reference}</span>}
                      <span className={jeBalanced ? 'text-green-600' : 'text-red-600'}>
                        د. {(Number(j.totalDebit) || 0).toLocaleString()} / ح. {(Number(j.totalCredit) || 0).toLocaleString()}
                      </span>
                    </div>
                  </div>
                  <div className="flex items-center gap-1">
                    <Link href={`/finance/journal-entries/${j.id}`}>
                      <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3 w-3" />} />
                    </Link>
                    <Badge variant={statusInfo.variant}>{statusInfo.label}</Badge>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
