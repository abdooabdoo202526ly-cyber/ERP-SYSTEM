'use client';

// شجرة الشركات (Companies Tree) — Holding → Subsidiaries

import { useEffect, useMemo, useState } from 'react';
import Link from 'next/link';
import {
  Plus,
  Building2,
  ChevronRight,
  Eye,
  Power,
  Pencil,
  RefreshCw,
  Layers,
} from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { companiesApi, CompanyTreeNode, getErrorMessage } from '@/lib/api';

// ============ Tree rendering ============

interface RenderTreeProps {
  node: CompanyTreeNode;
  depth: number;
  /** يحوّل node.company.id إلى expanded/collapsed */
  expanded: Set<string>;
  toggle: (id: string) => void;
  onDeactivate: (id: string, name: string) => void;
}

function TreeBranch({ node, depth, expanded, toggle, onDeactivate }: RenderTreeProps) {
  // الـ root-wrapper ليس له company — نعرض فقط الـ children.
  if (!node.company) {
    return (
      <>
        {node.children.map((child) => (
          <TreeBranch
            key={child.company?.id || Math.random()}
            node={child}
            depth={depth}
            expanded={expanded}
            toggle={toggle}
            onDeactivate={onDeactivate}
          />
        ))}
      </>
    );
  }

  const c = node.company;
  const hasChildren = node.children.length > 0;
  const isOpen = expanded.has(c.id);
  // في RTL: الـ indent يكون من اليمين
  const indent = depth * 24;

  return (
    <div>
      <div
        className="flex items-center gap-3 px-3 py-2.5 rounded-lg hover:bg-gray-50 group"
        style={{ marginInlineStart: indent }}
      >
        {/* Expand/collapse button */}
        <button
          type="button"
          onClick={() => hasChildren && toggle(c.id)}
          className={`h-6 w-6 flex items-center justify-center rounded ${
            hasChildren
              ? 'text-gray-500 hover:bg-gray-200 cursor-pointer'
              : 'text-gray-300 cursor-default'
          }`}
          aria-label={isOpen ? 'طي' : 'توسيع'}
          disabled={!hasChildren}
        >
          {hasChildren ? (
            <ChevronRight
              className={`h-4 w-4 transition-transform ${isOpen ? 'rotate-90' : ''}`}
            />
          ) : (
            <span className="h-1 w-1 rounded-full bg-gray-300" />
          )}
        </button>

        {/* Icon */}
        <div
          className={`h-8 w-8 rounded-lg flex items-center justify-center flex-shrink-0 ${
            c.isGroup ? 'bg-purple-100 text-purple-700' : 'bg-blue-100 text-blue-700'
          }`}
        >
          <Building2 className="h-4 w-4" />
        </div>

        {/* Info */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <Link
              href={`/admin/companies/${c.id}`}
              className="font-bold text-gray-800 hover:text-blue-600 truncate"
            >
              {c.name}
            </Link>
            <span className="text-xs text-gray-500 font-mono">{c.code}</span>
            <Badge variant={c.isGroup ? 'info' : 'neutral'}>
              {c.isGroup ? 'Holding' : 'Subsidiary'}
            </Badge>
            {!c.isActive && <Badge variant="warning">معطّلة</Badge>}
            {hasChildren && (
              <Badge variant="neutral">
                {node.children.length} شركة فرعية
              </Badge>
            )}
          </div>
          {c.legalName && c.legalName !== c.name && (
            <p className="text-xs text-gray-500 truncate">{c.legalName}</p>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-1 opacity-0 group-hover:opacity-100 transition-opacity">
          <Link href={`/admin/companies/${c.id}`}>
            <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3.5 w-3.5" />}>
              عرض
            </Button>
          </Link>
          {c.isActive && (
            <Link href={`/admin/companies/${c.id}/edit`}>
              <Button
                variant="ghost"
                size="sm"
                iconLeft={<Pencil className="h-3.5 w-3.5" />}
              >
                تعديل
              </Button>
            </Link>
          )}
          {c.isActive && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => onDeactivate(c.id, c.name)}
              iconLeft={<Power className="h-3.5 w-3.5 text-red-500" />}
            >
              <span className="text-red-600 text-xs">إيقاف</span>
            </Button>
          )}
        </div>
      </div>

      {/* Children */}
      {hasChildren && isOpen && (
        <div className="border-s-2 border-gray-200 ms-6">
          {node.children.map((child) => (
            <TreeBranch
              key={child.company?.id || Math.random()}
              node={child}
              depth={depth + 1}
              expanded={expanded}
              toggle={toggle}
              onDeactivate={onDeactivate}
            />
          ))}
        </div>
      )}
    </div>
  );
}

// ============ Page ============

export default function CompaniesPage() {
  const { loading: authLoading } = useAuth();
  const [tree, setTree] = useState<CompanyTreeNode | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [deactivating, setDeactivating] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await companiesApi.getTree();
      setTree(data);
      // افتح كل الـ Holdings افتراضياً
      const initial = new Set<string>();
      data.children.forEach((n) => {
        if (n.company) initial.add(n.company.id);
      });
      setExpanded(initial);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحميل الشركات.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const toggle = (id: string) => {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const expandAll = () => {
    if (!tree) return;
    const all = new Set<string>();
    const walk = (n: CompanyTreeNode) => {
      if (n.company) all.add(n.company.id);
      n.children.forEach(walk);
    };
    walk(tree);
    setExpanded(all);
  };

  const collapseAll = () => setExpanded(new Set());

  const onDeactivate = async (id: string, name: string) => {
    if (typeof window === 'undefined') return;
    if (!window.confirm(`هل تريد إيقاف الشركة "${name}"؟`)) return;
    setDeactivating(id);
    try {
      await companiesApi.deactivateCompany(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل إيقاف الشركة.'));
    } finally {
      setDeactivating(null);
    }
  };

  // إحصائيات سريعة
  const stats = useMemo(() => {
    if (!tree) return { holdings: 0, subs: 0, total: 0 };
    let holdings = 0;
    let subs = 0;
    const walk = (n: CompanyTreeNode) => {
      if (n.company) {
        if (n.company.isGroup) holdings++;
        else subs++;
      }
      n.children.forEach(walk);
    };
    walk(tree);
    return { holdings, subs, total: holdings + subs };
  }, [tree]);

  return (
    <div>
      <PageHeader
        title="🏢 الشركات"
        description="هيكل الشركات القابضة (Holding) والشركات التابعة (Subsidiaries)"
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={load}
              iconLeft={<RefreshCw className="h-4 w-4" />}
            >
              تحديث
            </Button>
            <Link href="/admin/companies/new">
              <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
                شركة جديدة
              </Button>
            </Link>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {/* Stats */}
      <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 mb-4">
        <Card className="!p-0">
          <div className="p-4 flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-purple-100 text-purple-700 flex items-center justify-center">
              <Layers className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs text-gray-500">شركات قابضة</p>
              <p className="text-xl font-bold text-gray-800">{stats.holdings}</p>
            </div>
          </div>
        </Card>
        <Card className="!p-0">
          <div className="p-4 flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-blue-100 text-blue-700 flex items-center justify-center">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs text-gray-500">شركات تابعة</p>
              <p className="text-xl font-bold text-gray-800">{stats.subs}</p>
            </div>
          </div>
        </Card>
        <Card className="!p-0">
          <div className="p-4 flex items-center gap-3">
            <div className="h-10 w-10 rounded-lg bg-gray-100 text-gray-700 flex items-center justify-center">
              <Building2 className="h-5 w-5" />
            </div>
            <div>
              <p className="text-xs text-gray-500">إجمالي</p>
              <p className="text-xl font-bold text-gray-800">{stats.total}</p>
            </div>
          </div>
        </Card>
      </div>

      {/* Tree card */}
      <Card
        title="🌳 الهيكل التنظيمي"
        description="اضغط على ▸ لتوسيع/طي الشركات الفرعية"
        actions={
          <div className="flex items-center gap-2">
            <Button variant="ghost" size="sm" onClick={expandAll}>
              توسيع الكل
            </Button>
            <Button variant="ghost" size="sm" onClick={collapseAll}>
              طي الكل
            </Button>
          </div>
        }
      >
        {loading ? (
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        ) : !tree || tree.children.length === 0 ? (
          <div className="text-center py-12 text-gray-500">
            <Building2 className="h-12 w-12 mx-auto text-gray-300 mb-3" />
            <p className="font-semibold">لا توجد شركات بعد</p>
            <p className="text-sm mt-1">ابدأ بإنشاء شركة قابضة (Holding)</p>
            <Link href="/admin/companies/new">
              <Button
                variant="primary"
                size="sm"
                className="mt-4"
                iconLeft={<Plus className="h-4 w-4" />}
              >
                إنشاء أول شركة
              </Button>
            </Link>
          </div>
        ) : (
          <div className="space-y-1">
            {tree.children.map((child) => (
              <TreeBranch
                key={child.company?.id || Math.random()}
                node={child}
                depth={0}
                expanded={expanded}
                toggle={toggle}
                onDeactivate={onDeactivate}
              />
            ))}
          </div>
        )}
      </Card>

      {deactivating && (
        <p className="text-xs text-gray-500 mt-2 text-center">جاري الإيقاف...</p>
      )}
    </div>
  );
}
