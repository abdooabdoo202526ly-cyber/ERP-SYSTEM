'use client';

// تعديل هيكل راتب — نفس حقول new لكن مع تحميل البيانات أولاً

import { useEffect, useState } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Save, Plus, Trash2, Banknote, MinusCircle, GripVertical } from 'lucide-react';
import { Button, Input, Card, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  salaryStructuresApi,
  SALARY_COMPONENT_TYPE_LABELS,
  CreateSalaryStructureRequest,
  getErrorMessage,
  SalaryStructure,
} from '@/lib/api';

type ComponentType = 1 | 2;

interface DraftLine {
  _key: string;
  id?: string; // existing line id (if any) — kept for reference but backend does full-replace
  type: ComponentType;
  name: string;
  formula: string;
  amount: string;
  sortOrder: number;
}

function emptyLine(sortOrder: number): DraftLine {
  return {
    _key: crypto.randomUUID(),
    type: 1,
    name: '',
    formula: '',
    amount: '0',
    sortOrder,
  };
}

function lineFromExisting(l: { id: string; type: number; name: string; formula?: string; amount: number; sortOrder: number }): DraftLine {
  return {
    _key: crypto.randomUUID(),
    id: l.id,
    type: (l.type === 2 ? 2 : 1) as ComponentType,
    name: l.name,
    formula: l.formula || '',
    amount: String(l.amount),
    sortOrder: l.sortOrder,
  };
}

export default function EditSalaryStructurePage() {
  const router = useRouter();
  const params = useParams<{ id: string }>();
  const id = params?.id;
  useAuth();

  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [isActive, setIsActive] = useState(true);
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [loading, setLoading] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load existing structure
  useEffect(() => {
    if (!id) return;
    setLoading(true);
    salaryStructuresApi
      .get(id)
      .then((s: SalaryStructure) => {
        setName(s.name);
        setCode(s.code);
        setCurrency(s.currency || 'LYD');
        setIsActive(s.isActive);
        const sortedLines = (s.lines || []).slice().sort((a, b) => a.sortOrder - b.sortOrder);
        setLines(sortedLines.length > 0 ? sortedLines.map(lineFromExisting) : [emptyLine(0)]);
      })
      .catch((e: unknown) => setError(getErrorMessage(e, 'فشل تحميل هيكل الراتب.')))
      .finally(() => setLoading(false));
  }, [id]);

  const onAddLine = () => {
    setLines((prev) => [...prev, emptyLine(prev.length)]);
  };

  const onRemoveLine = (key: string) => {
    setLines((prev) =>
      prev
        .filter((l) => l._key !== key)
        .map((l, idx) => ({ ...l, sortOrder: idx }))
    );
  };

  const onLineChange = (key: string, field: keyof DraftLine, value: string | number) => {
    setLines((prev) =>
      prev.map((l) => (l._key === key ? { ...l, [field]: value } : l))
    );
  };

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);

    if (!name.trim() || !code.trim()) {
      setError('الاسم والكود مطلوبان.');
      return;
    }
    for (const l of lines) {
      if (!l.name.trim()) {
        setError('كل مكوّن يجب أن يكون له اسم.');
        return;
      }
      if (Number(l.amount) < 0) {
        setError('قيمة المكوّن يجب أن تكون ≥ 0.');
        return;
      }
    }

    const req: CreateSalaryStructureRequest = {
      name: name.trim(),
      code: code.trim(),
      currency: currency.trim() || 'LYD',
      isActive,
      lines: lines.map((l) => ({
        type: l.type,
        name: l.name.trim(),
        formula: l.formula.trim() || undefined,
        amount: Number(l.amount),
        sortOrder: l.sortOrder,
      })),
    };

    setSubmitting(true);
    try {
      await salaryStructuresApi.update(id!, req);
      router.push(`/hr/salary-structures/${id}`);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث هيكل الراتب.'));
      setSubmitting(false);
    }
  };

  const totalEarnings = lines
    .filter((l) => l.type === 1)
    .reduce((sum, l) => sum + Number(l.amount || 0), 0);
  const totalDeductions = lines
    .filter((l) => l.type === 2)
    .reduce((sum, l) => sum + Number(l.amount || 0), 0);

  if (loading) {
    return (
      <div className="p-8 text-center text-gray-500">جاري التحميل…</div>
    );
  }

  return (
    <div>
      <PageHeader
        title="✏️ تعديل هيكل راتب"
        description={code ? `الكود: ${code}` : 'تعديل المكوّنات والخصائص'}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'هياكل الرواتب', href: '/hr/salary-structures' },
          { label: code || 'تعديل' },
        ]}
        actions={
          <Link href={`/hr/salary-structures/${id}`}>
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      <form onSubmit={onSubmit} className="space-y-4">
        {error && (
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error}
          </div>
        )}

        <Card title="📋 معلومات الهيكل">
          <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
            <Input
              label="الاسم *"
              value={name}
              onChange={(e) => setName(e.target.value)}
              required
              placeholder="مثال: هيكل بدوام كامل"
            />
            <Input
              label="الكود *"
              value={code}
              onChange={(e) => setCode(e.target.value)}
              required
              placeholder="FT-LYD"
            />
            <Input
              label="العملة"
              value={currency}
              onChange={(e) => setCurrency(e.target.value.toUpperCase())}
              maxLength={3}
              placeholder="LYD"
            />
            <div>
              <label className="block text-sm font-medium text-gray-700 mb-1">الحالة</label>
              <label className="flex items-center gap-2 mt-2 text-sm">
                <input
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="rounded"
                />
                نشط
              </label>
            </div>
          </div>
        </Card>

        <Card
          title={
            <span>
              🧩 المكوّنات (Lines)
              <span className="text-gray-400 text-sm font-normal me-2">
                ({lines.length} سطر)
              </span>
            </span>
          }
          description="مستحقات (+) ومستقطعات (-). السطر 0 = الراتب الأساسي عادة."
          actions={
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={onAddLine}
              iconLeft={<Plus className="h-4 w-4" />}
            >
              إضافة مكوّن
            </Button>
          }
        >
          {lines.length === 0 ? (
            <div className="text-center py-8 text-gray-500">
              <Banknote className="h-10 w-10 mx-auto text-gray-300 mb-2" />
              <p className="text-sm">لا توجد مكوّنات بعد. أضف الراتب الأساسي أولاً.</p>
            </div>
          ) : (
            <div className="space-y-2">
              {lines.map((line, idx) => (
                <div
                  key={line._key}
                  className={`flex items-start gap-2 p-3 rounded-lg border ${
                    line.type === 1
                      ? 'bg-green-50 border-green-200'
                      : 'bg-red-50 border-red-200'
                  }`}
                >
                  <div className="pt-2 text-gray-400">
                    <GripVertical className="h-4 w-4" />
                  </div>
                  <div className="flex-shrink-0 w-8 text-center pt-1 text-xs font-mono text-gray-500">
                    {idx}
                  </div>
                  <div className="flex-1 grid grid-cols-1 md:grid-cols-12 gap-2">
                    <div className="md:col-span-3">
                      <label className="block text-xs text-gray-500 mb-1">النوع</label>
                      <select
                        value={line.type}
                        onChange={(e) =>
                          onLineChange(line._key, 'type', Number(e.target.value) as ComponentType)
                        }
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm bg-white"
                      >
                        <option value={1}>+ مستحق (Earning)</option>
                        <option value={2}>− مستقطع (Deduction)</option>
                      </select>
                    </div>
                    <div className="md:col-span-4">
                      <label className="block text-xs text-gray-500 mb-1">الاسم *</label>
                      <input
                        value={line.name}
                        onChange={(e) => onLineChange(line._key, 'name', e.target.value)}
                        required
                        placeholder="الراتب الأساسي"
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm bg-white"
                      />
                    </div>
                    <div className="md:col-span-3">
                      <label className="block text-xs text-gray-500 mb-1">المبلغ *</label>
                      <input
                        type="number"
                        step="0.0001"
                        min="0"
                        value={line.amount}
                        onChange={(e) => onLineChange(line._key, 'amount', e.target.value)}
                        required
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-sm font-mono bg-white"
                      />
                    </div>
                    <div className="md:col-span-2">
                      <label className="block text-xs text-gray-500 mb-1">Formula</label>
                      <input
                        value={line.formula}
                        onChange={(e) => onLineChange(line._key, 'formula', e.target.value)}
                        placeholder="base * 0.1"
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-xs bg-white"
                      />
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => onRemoveLine(line._key)}
                    className="pt-1.5 text-red-500 hover:text-red-700"
                    aria-label="حذف السطر"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>
          )}

          {lines.length > 0 && (
            <div className="mt-4 pt-4 border-t grid grid-cols-2 gap-4 text-sm">
              <div className="flex items-center justify-between p-3 rounded-lg bg-green-50">
                <span className="text-gray-700 font-semibold flex items-center gap-1">
                  <Plus className="h-3 w-3" /> إجمالي المستحقات
                </span>
                <span className="font-mono font-bold text-green-700">
                  {totalEarnings.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </span>
              </div>
              <div className="flex items-center justify-between p-3 rounded-lg bg-red-50">
                <span className="text-gray-700 font-semibold flex items-center gap-1">
                  <MinusCircle className="h-3 w-3" /> إجمالي المستقطعات
                </span>
                <span className="font-mono font-bold text-red-600">
                  {totalDeductions.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                </span>
              </div>
            </div>
          )}
        </Card>

        <div className="flex items-center gap-2 pt-2">
          <Button
            type="submit"
            variant="primary"
            loading={submitting}
            iconLeft={<Save className="h-4 w-4" />}
          >
            حفظ التغييرات
          </Button>
          <Link href={`/hr/salary-structures/${id}`}>
            <Button type="button" variant="ghost">
              إلغاء
            </Button>
          </Link>
        </div>
      </form>
    </div>
  );
}
