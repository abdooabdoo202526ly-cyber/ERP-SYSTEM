'use client';

// تفاصيل هيكل راتب (Salary Structure Detail) — عرض + تعديل + إيقاف

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Save,
  Plus,
  Trash2,
  Banknote,
  Power,
  Hash,
  CheckCircle2,
  XCircle,
  Pencil,
  MinusCircle,
  GripVertical,
  Calendar,
  Eye,
} from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  salaryStructuresApi,
  SalaryStructure,
  SalaryStructureLine,
  CreateSalaryStructureRequest,
  SALARY_COMPONENT_TYPE_LABELS,
  getErrorMessage,
} from '@/lib/api';

type ComponentType = 1 | 2;

interface DraftLine {
  _key: string;
  type: ComponentType;
  name: string;
  formula: string;
  amount: string;
  sortOrder: number;
}

function toDraft(ln: SalaryStructureLine, idx: number): DraftLine {
  return {
    _key: crypto.randomUUID(),
    type: (ln.type as unknown) as ComponentType,
    name: ln.name,
    formula: ln.formula || '',
    amount: String(ln.amount),
    sortOrder: idx,
  };
}

function formatDate(s?: string): string {
  if (!s) return '-';
  try {
    return new Date(s).toLocaleString('ar-LY', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
    });
  } catch {
    return s;
  }
}

export default function SalaryStructureDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const [structure, setStructure] = useState<SalaryStructure | null>(null);
  const [editing, setEditing] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deactivating, setDeactivating] = useState(false);

  // form state عند التعديل
  const [name, setName] = useState('');
  const [code, setCode] = useState('');
  const [currency, setCurrency] = useState('LYD');
  const [isActive, setIsActive] = useState(true);
  const [lines, setLines] = useState<DraftLine[]>([]);
  const [submitting, setSubmitting] = useState(false);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const s = await salaryStructuresApi.get(params.id);
      setStructure(s);
      // hydrate form
      setName(s.name);
      setCode(s.code);
      setCurrency(s.currency);
      setIsActive(s.isActive);
      setLines(s.lines.map((l, i) => toDraft(l, i)));
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل هيكل الراتب.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!structure) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف هيكل الراتب "${structure.name}"؟`)) return;
    setDeactivating(true);
    try {
      await salaryStructuresApi.deactivate(structure.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف هيكل الراتب.'));
    } finally {
      setDeactivating(false);
    }
  };

  const onAddLine = () => {
    setLines((prev) => [...prev, {
      _key: crypto.randomUUID(),
      type: 1,
      name: '',
      formula: '',
      amount: '0',
      sortOrder: prev.length,
    }]);
  };

  const onRemoveLine = (key: string) => {
    setLines((prev) =>
      prev.filter((l) => l._key !== key).map((l, idx) => ({ ...l, sortOrder: idx }))
    );
  };

  const onLineChange = (key: string, field: keyof DraftLine, value: string | number) => {
    setLines((prev) =>
      prev.map((l) => (l._key === key ? { ...l, [field]: value } : l))
    );
  };

  const onSaveEdit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!structure) return;
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
      const updated = await salaryStructuresApi.update(structure.id, req);
      setStructure(updated);
      setEditing(false);
      setLines(updated.lines.map((l, i) => toDraft(l, i)));
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث هيكل الراتب.'));
    } finally {
      setSubmitting(false);
    }
  };

  const onCancelEdit = () => {
    if (!structure) return;
    setName(structure.name);
    setCode(structure.code);
    setCurrency(structure.currency);
    setIsActive(structure.isActive);
    setLines(structure.lines.map((l, i) => toDraft(l, i)));
    setEditing(false);
    setError(null);
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="هيكل راتب" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!structure) {
    return (
      <div>
        <PageHeader
          title="هيكل راتب"
          actions={
            <Link href="/hr/salary-structures">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'هيكل الراتب غير موجود.'}
          </div>
          <div className="mt-4">
            <Link href="/hr/salary-structures">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  const totalEarnings = lines
    .filter((l) => l.type === 1)
    .reduce((sum, l) => sum + Number(l.amount || 0), 0);
  const totalDeductions = lines
    .filter((l) => l.type === 2)
    .reduce((sum, l) => sum + Number(l.amount || 0), 0);

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div
              className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                structure.isActive
                  ? 'bg-emerald-100 text-emerald-700'
                  : 'bg-gray-100 text-gray-500'
              }`}
            >
              <Banknote className="h-5 w-5" />
            </div>
            <div>
              <span>{structure.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {structure.code}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            <Badge variant="info">{structure.currency}</Badge>
            {structure.isActive ? (
              <Badge variant="success">نشط</Badge>
            ) : (
              <Badge variant="warning">معطّل</Badge>
            )}
            <span className="text-gray-400">•</span>
            <span>{structure.lines.length} مكوّن</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'هياكل الرواتب', href: '/hr/salary-structures' },
          { label: structure.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/hr/salary-structures">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            {!editing && structure.isActive && (
              <Button
                variant="primary"
                onClick={() => setEditing(true)}
                iconLeft={<Pencil className="h-4 w-4" />}
              >
                تعديل
              </Button>
            )}
            {!editing && structure.isActive && (
              <Button
                variant="ghost"
                onClick={onDeactivate}
                loading={deactivating}
                iconLeft={<Power className="h-4 w-4 text-red-500" />}
              >
                <span className="text-red-600 text-sm">إيقاف</span>
              </Button>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {editing ? (
        // === وضع التعديل ===
        <form onSubmit={onSaveEdit} className="space-y-4">
          <Card title="📋 معلومات الهيكل">
            <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
              <Input
                label="الاسم *"
                value={name}
                onChange={(e) => setName(e.target.value)}
                required
              />
              <Input
                label="الكود *"
                value={code}
                onChange={(e) => setCode(e.target.value)}
                required
              />
              <Input
                label="العملة"
                value={currency}
                onChange={(e) => setCurrency(e.target.value.toUpperCase())}
                maxLength={3}
              />
              <label className="flex items-center gap-2 text-sm text-gray-700 pt-6">
                <input
                  type="checkbox"
                  checked={isActive}
                  onChange={(e) => setIsActive(e.target.checked)}
                  className="h-4 w-4 rounded border-gray-300 text-blue-600 focus:ring-blue-500"
                />
                <span>نشط</span>
              </label>
            </div>
          </Card>

          <Card
            title="🧩 المكوّنات (Lines)"
            description="سيتم استبدال كل المكوّنات بالحالي (full-replace)"
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
                        className="w-full border border-gray-300 rounded px-2 py-1.5 text-xs bg-white"
                      />
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => onRemoveLine(line._key)}
                    className="pt-1.5 text-red-500 hover:text-red-700"
                  >
                    <Trash2 className="h-4 w-4" />
                  </button>
                </div>
              ))}
            </div>

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
          </Card>

          <div className="flex items-center gap-2 pt-2">
            <Button
              type="submit"
              variant="primary"
              loading={submitting}
              iconLeft={<Save className="h-4 w-4" />}
            >
              حفظ التعديلات
            </Button>
            <Button
              type="button"
              variant="ghost"
              onClick={onCancelEdit}
              iconLeft={<Eye className="h-4 w-4" />}
            >
              إلغاء التعديل
            </Button>
          </div>
        </form>
      ) : (
        // === وضع العرض ===
        <div className="space-y-4">
          <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <Card title="📋 المعلومات الأساسية" className="lg:col-span-2">
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                    <Hash className="h-4 w-4" /> الكود
                  </p>
                  <p className="font-mono">{structure.code}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-500 mb-1">اسم الهيكل</p>
                  <p>{structure.name}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-500 mb-1">العملة</p>
                  <Badge variant="info">{structure.currency}</Badge>
                </div>
                <div>
                  <p className="text-xs text-gray-500 mb-1">الحالة</p>
                  {structure.isActive ? (
                    <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                      <CheckCircle2 className="h-4 w-4" /> نشط
                    </span>
                  ) : (
                    <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                      <XCircle className="h-4 w-4" /> معطّل
                    </span>
                  )}
                </div>
                <div>
                  <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                    <Calendar className="h-4 w-4" /> تاريخ الإنشاء
                  </p>
                  <p className="text-xs">{formatDate(structure.createdAt)}</p>
                </div>
                <div>
                  <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                    <Calendar className="h-4 w-4" /> آخر تحديث
                  </p>
                  <p className="text-xs">{formatDate(structure.updatedAt)}</p>
                </div>
              </div>
            </Card>

            <Card title="📊 الإحصائيات">
              <div className="space-y-3">
                <div className="flex items-center justify-between p-3 rounded-lg bg-green-50">
                  <span className="text-sm text-gray-600">إجمالي المستحقات</span>
                  <span className="font-mono font-bold text-green-700">
                    {structure.totalEarnings.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="flex items-center justify-between p-3 rounded-lg bg-red-50">
                  <span className="text-sm text-gray-600">إجمالي المستقطعات</span>
                  <span className="font-mono font-bold text-red-600">
                    {structure.totalDeductions.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                  </span>
                </div>
                <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
                  <span className="text-sm text-gray-600">عدد المكوّنات</span>
                  <span className="font-mono">{structure.lines.length}</span>
                </div>
              </div>
            </Card>
          </div>

          <Card
            title={
              <span>
                🧩 المكوّنات (Lines)
                {structure.lines.length > 0 && (
                  <span className="text-gray-400 text-sm font-normal me-2">
                    ({structure.lines.length})
                  </span>
                )}
              </span>
            }
          >
            {structure.lines.length === 0 ? (
              <div className="text-center py-8 text-gray-500">
                <Banknote className="h-10 w-10 mx-auto text-gray-300 mb-2" />
                <p className="text-sm">لا توجد مكوّنات لهذا الهيكل.</p>
              </div>
            ) : (
              <div className="space-y-2">
                {structure.lines.map((l, i) => (
                  <div
                    key={l.id}
                    className={`flex items-center gap-3 p-3 rounded-lg border ${
                      l.type === 1
                        ? 'bg-green-50 border-green-200'
                        : 'bg-red-50 border-red-200'
                    }`}
                  >
                    <div className="flex-shrink-0 w-8 text-center text-xs font-mono text-gray-500">
                      #{i}
                    </div>
                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <span className="font-semibold text-gray-800">{l.name}</span>
                        <Badge variant={l.type === 1 ? 'success' : 'danger'}>
                          {SALARY_COMPONENT_TYPE_LABELS[l.type]}
                        </Badge>
                        {l.formula && (
                          <span className="text-xs font-mono text-gray-500">
                            formula: {l.formula}
                          </span>
                        )}
                      </div>
                    </div>
                    <div className="font-mono font-bold text-base text-gray-800" dir="ltr">
                      {l.type === 1 ? '+' : '−'}
                      {l.amount.toLocaleString(undefined, { minimumFractionDigits: 2 })}
                    </div>
                  </div>
                ))}
              </div>
            )}
          </Card>
        </div>
      )}
    </div>
  );
}
