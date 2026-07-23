'use client';

// إنشاء قيد محاسبي جديد (Journal Entry)

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Plus, Trash2 } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface AccountOption {
  id: string;
  code: string;
  name: string;
}

interface LineDraft {
  accountId: string;
  debit: string;
  credit: string;
  description: string;
}

interface FormState {
  entryDate: string;
  description: string;
  reference: string;
  lines: LineDraft[];
}

const emptyLine = (): LineDraft => ({ accountId: '', debit: '0', credit: '0', description: '' });

export default function NewJournalEntryPage() {
  const router = useRouter();
  useAuth();
  const [form, setForm] = useState<FormState>({
    entryDate: new Date().toISOString().split('T')[0],
    description: '',
    reference: '',
    lines: [emptyLine(), emptyLine()],
  });
  const [accounts, setAccounts] = useState<AccountOption[]>([]);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadAccounts = async () => {
      try {
        const res = await fetch('/api/finance/accounts');
        if (!res.ok) return;
        const data = await res.json();
        setAccounts(data.map((a: AccountOption) => ({ id: a.id, code: a.code, name: a.name })));
      } catch {
        // ignore
      }
    };
    loadAccounts();
  }, []);

  const totalDebit = form.lines.reduce((s, l) => s + (Number(l.debit) || 0), 0);
  const totalCredit = form.lines.reduce((s, l) => s + (Number(l.credit) || 0), 0);
  const balanced = Math.abs(totalDebit - totalCredit) < 0.01 && totalDebit > 0;

  const updateLine = (idx: number, field: keyof LineDraft, value: string) => {
    setForm((f) => {
      const newLines = [...f.lines];
      newLines[idx] = { ...newLines[idx], [field]: value };
      return { ...f, lines: newLines };
    });
  };

  const addLine = () => {
    setForm((f) => ({ ...f, lines: [...f.lines, emptyLine()] }));
  };

  const removeLine = (idx: number) => {
    if (form.lines.length <= 2) return;
    setForm((f) => ({ ...f, lines: f.lines.filter((_, i) => i !== idx) }));
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!balanced) {
      setError(`القيد غير متوازن: مدين=${totalDebit.toFixed(2)}, دائن=${totalCredit.toFixed(2)}`);
      return;
    }
    setError(null);
    setSubmitting(true);
    try {
      const res = await fetch('/api/finance/journal-entries', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          entryDate: form.entryDate,
          description: form.description,
          reference: form.reference || null,
          lines: form.lines.map((l) => ({
            accountId: l.accountId,
            debit: Number(l.debit) || 0,
            credit: Number(l.credit) || 0,
            description: l.description || null,
          })),
        }),
      });
      if (!res.ok) {
        const t = await res.text();
        throw new Error(t || 'فشل إنشاء القيد');
      }
      const created = await res.json();
      router.push(`/finance/journal-entries/${created.id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إنشاء القيد.'));
      setSubmitting(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="➕ قيد محاسبي جديد"
        description="أنشئ قيد يومية (يجب أن يكون متوازن: مدين = دائن)"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'القيود', href: '/finance/journal-entries' },
          { label: 'جديد' },
        ]}
        actions={
          <Link href="/finance/journal-entries">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
          </Link>
        }
      />

      <Card className="max-w-4xl">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
        )}

        <form onSubmit={onSubmit} className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Input label="التاريخ *" type="date" value={form.entryDate} onChange={(e) => setForm({ ...form, entryDate: e.target.value })} required />
            <Input label="المرجع" value={form.reference} onChange={(e) => setForm({ ...form, reference: e.target.value })} placeholder="اختياري" />
          </div>

          <Input label="الوصف *" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} required placeholder="مثال: فاتورة كهرباء" />

          {/* Lines */}
          <div className="border-t pt-4">
            <div className="flex items-center justify-between mb-3">
              <h3 className="font-bold text-gray-800">البنود (Lines)</h3>
              <Button type="button" variant="ghost" size="sm" onClick={addLine} iconLeft={<Plus className="h-4 w-4" />}>
                إضافة بند
              </Button>
            </div>

            <div className="space-y-3">
              {form.lines.map((line, idx) => (
                <div key={idx} className="border rounded-lg p-3 bg-gray-50">
                  <div className="grid grid-cols-12 gap-2">
                    <div className="col-span-6">
                      <label className="block text-xs text-gray-500 mb-1">الحساب</label>
                      <select
                        value={line.accountId}
                        onChange={(e) => updateLine(idx, 'accountId', e.target.value)}
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
                        required
                      >
                        <option value="">-- اختر --</option>
                        {accounts.map((a) => (
                          <option key={a.id} value={a.id}>
                            {a.code} - {a.name}
                          </option>
                        ))}
                      </select>
                    </div>
                    <div className="col-span-2">
                      <label className="block text-xs text-gray-500 mb-1">مدين</label>
                      <input
                        type="number"
                        step="0.01"
                        value={line.debit}
                        onChange={(e) => updateLine(idx, 'debit', e.target.value)}
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm font-mono"
                      />
                    </div>
                    <div className="col-span-2">
                      <label className="block text-xs text-gray-500 mb-1">دائن</label>
                      <input
                        type="number"
                        step="0.01"
                        value={line.credit}
                        onChange={(e) => updateLine(idx, 'credit', e.target.value)}
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm font-mono"
                      />
                    </div>
                    <div className="col-span-2 flex items-end">
                      {form.lines.length > 2 && (
                        <button
                          type="button"
                          onClick={() => removeLine(idx)}
                          className="text-red-600 hover:bg-red-50 p-1.5 rounded"
                          aria-label="حذف البند"
                        >
                          <Trash2 className="h-4 w-4" />
                        </button>
                      )}
                    </div>
                    <div className="col-span-12">
                      <label className="block text-xs text-gray-500 mb-1">الوصف</label>
                      <input
                        type="text"
                        value={line.description}
                        onChange={(e) => updateLine(idx, 'description', e.target.value)}
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm"
                        placeholder="اختياري"
                      />
                    </div>
                  </div>
                </div>
              ))}
            </div>

            {/* Totals */}
            <div className="mt-4 p-3 bg-gray-100 rounded-lg flex items-center justify-between">
              <div className="text-sm font-bold">
                مدين: <span className="font-mono text-blue-600">{totalDebit.toFixed(2)}</span>
              </div>
              <div className="text-sm font-bold">
                دائن: <span className="font-mono text-orange-600">{totalCredit.toFixed(2)}</span>
              </div>
              <div className={`text-sm font-bold ${balanced ? 'text-green-600' : 'text-red-600'}`}>
                {balanced ? '✅ متوازن' : `❌ فرق ${Math.abs(totalDebit - totalCredit).toFixed(2)}`}
              </div>
            </div>
          </div>

          <div className="flex items-center gap-2 pt-3 border-t">
            <Button type="submit" variant="primary" loading={submitting} disabled={!balanced} iconLeft={<Save className="h-4 w-4" />}>
              حفظ القيد (مسودة)
            </Button>
            <Link href="/finance/journal-entries">
              <Button type="button" variant="ghost">إلغاء</Button>
            </Link>
          </div>
        </form>
      </Card>
    </div>
  );
}
