'use client';

// تفاصيل شركة (Company Detail) — يعرض المعلومات + قائمة الـ subsidiaries
// يدعم إضافة subsidiary جديدة من نفس الصفحة

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Plus,
  Power,
  Building2,
  Layers,
  Hash,
  Globe2,
  Calendar,
  CheckCircle2,
  XCircle,
  Save,
} from 'lucide-react';
import { Card, Badge, PageHeader, Button, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  companiesApi,
  Company,
  CompanyTreeNode,
  getErrorMessage,
} from '@/lib/api';

// ============ Helpers ============

function formatDate(s: string): string {
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

// ابحث عن عقدة شركة بالشركة (عودية عبر tree)
function findNode(
  root: CompanyTreeNode,
  id: string
): CompanyTreeNode | null {
  if (root.company?.id === id) return root;
  for (const c of root.children) {
    const f = findNode(c, id);
    if (f) return f;
  }
  return null;
}

function flattenTree(root: CompanyTreeNode): Company[] {
  const out: Company[] = [];
  const walk = (n: CompanyTreeNode) => {
    if (n.company) out.push(n.company);
    n.children.forEach(walk);
  };
  walk(root);
  return out;
}

// ============ Page ============

export default function CompanyDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const [company, setCompany] = useState<Company | null>(null);
  const [parent, setParent] = useState<Company | null>(null);
  const [subsidiaries, setSubsidiaries] = useState<Company[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [deactivating, setDeactivating] = useState(false);
  const [showAddSub, setShowAddSub] = useState(false);
  const [subForm, setSubForm] = useState({ code: '', name: '', legalName: '' });
  const [subSubmitting, setSubSubmitting] = useState(false);
  const [subError, setSubError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const [c, subs, tree] = await Promise.all([
        companiesApi.getCompany(params.id),
        companiesApi.getSubsidiaries(params.id).catch(() => [] as Company[]),
        companiesApi.getTree(),
      ]);
      setCompany(c);
      setSubsidiaries(subs);
      // ابحث عن الشركة الأم في الـ tree
      const node = findNode(tree, params.id);
      if (node && node.company?.parentCompanyId) {
        const flat = flattenTree(tree);
        const p = flat.find((x) => x.id === node.company!.parentCompanyId);
        setParent(p || null);
      } else {
        setParent(null);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل الشركة.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!company) return;
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف الشركة "${company.name}"؟`)) return;
    setDeactivating(true);
    try {
      await companiesApi.deactivateCompany(company.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف الشركة.'));
    } finally {
      setDeactivating(false);
    }
  };

  const onAddSubsidiary = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!company) return;
    setSubError(null);
    setSubSubmitting(true);
    try {
      await companiesApi.addSubsidiary({
        parentCompanyId: company.id,
        code: subForm.code,
        name: subForm.name,
        legalName: subForm.legalName || undefined,
      });
      // Reset
      setSubForm({ code: '', name: '', legalName: '' });
      setShowAddSub(false);
      await load();
    } catch (e: unknown) {
      setSubError(getErrorMessage(e, 'فشل إنشاء الشركة الفرعية.'));
    } finally {
      setSubSubmitting(false);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="شركة" />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!company) {
    return (
      <div>
        <PageHeader
          title="شركة"
          actions={
            <Link href="/admin/companies">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <Card>
          <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
            {error || 'الشركة غير موجودة.'}
          </div>
          <div className="mt-4">
            <Link href="/admin/companies">
              <Button variant="ghost">الرجوع للقائمة</Button>
            </Link>
          </div>
        </Card>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={
          <div className="flex items-center gap-3">
            <div
              className={`h-10 w-10 rounded-lg flex items-center justify-center ${
                company.isGroup
                  ? 'bg-purple-100 text-purple-700'
                  : 'bg-blue-100 text-blue-700'
              }`}
            >
              {company.isGroup ? (
                <Layers className="h-5 w-5" />
              ) : (
                <Building2 className="h-5 w-5" />
              )}
            </div>
            <div>
              <span>{company.name}</span>
              <p className="text-xs text-gray-500 font-mono font-normal mt-0.5">
                {company.code}
              </p>
            </div>
          </div>
        }
        description={
          <span className="flex items-center gap-2 flex-wrap">
            <Badge variant={company.isGroup ? 'info' : 'neutral'}>
              {company.isGroup ? 'Holding' : 'Subsidiary'}
            </Badge>
            {company.isActive ? (
              <Badge variant="success">فعّالة</Badge>
            ) : (
              <Badge variant="warning">معطّلة</Badge>
            )}
            <span className="text-gray-400">•</span>
            <span>العملة: {company.baseCurrency}</span>
          </span>
        }
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الشركات', href: '/admin/companies' },
          { label: company.name },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Link href="/admin/companies">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
            {company.isActive && (
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

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* المعلومات الأساسية */}
        <Card title="📋 المعلومات الأساسية" className="lg:col-span-2">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
            <FieldRow icon={<Hash className="h-4 w-4" />} label="الكود" value={company.code} mono />
            <FieldRow
              icon={<Building2 className="h-4 w-4" />}
              label="الاسم"
              value={company.name}
            />
            <FieldRow
              icon={<Globe2 className="h-4 w-4" />}
              label="العملة الأساسية"
              value={company.baseCurrency}
              mono
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="تاريخ الإنشاء"
              value={formatDate(company.createdAt)}
            />
            <FieldRow
              icon={<Calendar className="h-4 w-4" />}
              label="آخر تحديث"
              value={formatDate(company.updatedAt)}
            />
            <div>
              <p className="text-xs text-gray-500 mb-1">الاسم القانوني</p>
              <p className="text-sm">{company.legalName || '-'}</p>
            </div>
            <div>
              <p className="text-xs text-gray-500 mb-1">الحالة</p>
              {company.isActive ? (
                <span className="inline-flex items-center gap-1 text-green-700 text-sm">
                  <CheckCircle2 className="h-4 w-4" /> فعّالة
                </span>
              ) : (
                <span className="inline-flex items-center gap-1 text-yellow-700 text-sm">
                  <XCircle className="h-4 w-4" /> معطّلة
                </span>
              )}
            </div>
          </div>

          {/* Parent company */}
          <div className="mt-4 pt-4 border-t">
            <p className="text-xs text-gray-500 mb-2">الشركة الأم</p>
            {parent ? (
              <Link
                href={`/admin/companies/${parent.id}`}
                className="inline-flex items-center gap-2 px-3 py-2 rounded-lg bg-purple-50 border border-purple-200 hover:bg-purple-100 text-sm"
              >
                <Layers className="h-4 w-4 text-purple-600" />
                <span className="font-mono text-xs text-gray-500">{parent.code}</span>
                <span className="font-semibold text-purple-800">{parent.name}</span>
              </Link>
            ) : company.isGroup ? (
              <p className="text-sm text-gray-500">— لا يوجد (هذه شركة جذر) —</p>
            ) : (
              <p className="text-sm text-gray-500">— غير معروف —</p>
            )}
          </div>
        </Card>

        {/* الإحصائيات */}
        <Card title="📊 إحصائيات سريعة">
          <div className="space-y-3">
            <div className="flex items-center justify-between p-3 rounded-lg bg-blue-50">
              <span className="text-sm text-gray-600">الشركات الفرعية المباشرة</span>
              <span className="text-2xl font-bold text-blue-700">
                {subsidiaries.length}
              </span>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">النوع</span>
              <Badge variant={company.isGroup ? 'info' : 'neutral'}>
                {company.isGroup ? 'Holding' : 'Subsidiary'}
              </Badge>
            </div>
            <div className="flex items-center justify-between p-3 rounded-lg bg-gray-50">
              <span className="text-sm text-gray-600">المعرّف</span>
              <span className="text-xs font-mono text-gray-700" dir="ltr">
                {company.id.substring(0, 8)}…
              </span>
            </div>
          </div>
        </Card>
      </div>

      {/* Subsidiaries */}
      <Card
        className="mt-4"
        title={
          <span>
            🏢 الشركات الفرعية
            {subsidiaries.length > 0 && (
              <span className="text-gray-400 text-sm font-normal me-2">
                ({subsidiaries.length})
              </span>
            )}
          </span>
        }
        actions={
          company.isActive && company.isGroup ? (
            <Button
              variant="primary"
              size="sm"
              iconLeft={<Plus className="h-4 w-4" />}
              onClick={() => setShowAddSub((v) => !v)}
            >
              {showAddSub ? 'إلغاء' : 'إضافة شركة فرعية'}
            </Button>
          ) : null
        }
      >
        {showAddSub && (
          <form
            onSubmit={onAddSubsidiary}
            className="mb-4 p-4 rounded-lg bg-blue-50 border border-blue-200 space-y-3"
          >
            <h4 className="font-bold text-sm text-blue-900">➕ شركة فرعية جديدة</h4>
            {subError && (
              <div className="bg-red-50 border border-red-200 text-red-700 px-3 py-2 rounded text-xs">
                {subError}
              </div>
            )}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              <Input
                label="الكود *"
                value={subForm.code}
                onChange={(e) => setSubForm((f) => ({ ...f, code: e.target.value }))}
                required
                placeholder="SUB-001"
              />
              <Input
                label="الاسم *"
                value={subForm.name}
                onChange={(e) => setSubForm((f) => ({ ...f, name: e.target.value }))}
                required
                placeholder="اسم الشركة الفرعية"
              />
            </div>
            <Input
              label="الاسم القانوني"
              value={subForm.legalName}
              onChange={(e) =>
                setSubForm((f) => ({ ...f, legalName: e.target.value }))
              }
              placeholder="اختياري"
            />
            <div className="flex items-center gap-2 pt-2">
              <Button
                type="submit"
                variant="primary"
                size="sm"
                loading={subSubmitting}
                iconLeft={<Save className="h-4 w-4" />}
              >
                حفظ
              </Button>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => {
                  setShowAddSub(false);
                  setSubForm({ code: '', name: '', legalName: '' });
                  setSubError(null);
                }}
              >
                إلغاء
              </Button>
            </div>
          </form>
        )}

        {subsidiaries.length === 0 ? (
          <div className="text-center py-8 text-gray-500">
            <Building2 className="h-10 w-10 mx-auto text-gray-300 mb-2" />
            <p className="text-sm">لا توجد شركات فرعية مباشرة.</p>
            {company.isGroup && company.isActive && !showAddSub && (
              <p className="text-xs mt-1 text-gray-400">
                اضغط «إضافة شركة فرعية» أعلاه.
              </p>
            )}
            {!company.isGroup && (
              <p className="text-xs mt-1 text-gray-400">
                هذه الشركة تابعة — لا يمكن إضافة شركات تحتها.
              </p>
            )}
          </div>
        ) : (
          <div className="space-y-2">
            {subsidiaries.map((sub) => (
              <Link
                key={sub.id}
                href={`/admin/companies/${sub.id}`}
                className="flex items-center gap-3 p-3 rounded-lg border border-gray-100 hover:bg-blue-50 hover:border-blue-200 transition-colors"
              >
                <div
                  className={`h-8 w-8 rounded-lg flex items-center justify-center flex-shrink-0 ${
                    sub.isActive
                      ? 'bg-blue-100 text-blue-700'
                      : 'bg-gray-100 text-gray-400'
                  }`}
                >
                  <Building2 className="h-4 w-4" />
                </div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 flex-wrap">
                    <span className="font-semibold text-gray-800">{sub.name}</span>
                    <span className="text-xs text-gray-500 font-mono">{sub.code}</span>
                    {!sub.isActive && <Badge variant="warning">معطّلة</Badge>}
                  </div>
                  {sub.legalName && sub.legalName !== sub.name && (
                    <p className="text-xs text-gray-500 truncate">{sub.legalName}</p>
                  )}
                </div>
                <span className="text-xs text-gray-400 font-mono">
                  {sub.baseCurrency}
                </span>
              </Link>
            ))}
          </div>
        )}
      </Card>
    </div>
  );
}

// ============ Local subcomponents ============

interface FieldRowProps {
  icon: React.ReactNode;
  label: string;
  value: string;
  mono?: boolean;
}

function FieldRow({ icon, label, value, mono }: FieldRowProps) {
  return (
    <div>
      <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
        {icon} {label}
      </p>
      <p className={`text-sm ${mono ? 'font-mono' : ''}`}>{value}</p>
    </div>
  );
}
