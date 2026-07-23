'use client';

// صفحة دليل الحسابات (Chart of Accounts) — v1.0.32: pagination + search + type filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus } from 'lucide-react';
import { Table, Badge, PageHeader, Button, EntityActions, SearchBar, Pagination, Select } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { financeApi, getErrorMessage, Account, ACCOUNT_TYPES } from '@/lib/api';

export default function AccountsPage() {
  const { loading: authLoading } = useAuth();
  const [accounts, setAccounts] = useState<Account[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // v1.0.32: filters + pagination
  const [search, setSearch] = useState('');
  const [typeFilter, setTypeFilter] = useState<string>('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  useEffect(() => { setPage(1); }, [search, typeFilter]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await financeApi.listAccounts();
      setAccounts(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الحسابات.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return accounts.filter((a) => {
      if (typeFilter && String(a.type) !== typeFilter) return false;
      if (!q) return true;
      return (
        (a.code || '').toLowerCase().includes(q) ||
        (a.name || '').toLowerCase().includes(q) ||
        (a.description || '').toLowerCase().includes(q)
      );
    });
  }, [accounts, search, typeFilter]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="📊 دليل الحسابات"
        description="شجرة الحسابات (Chart of Accounts)"
        actions={
          <Link href="/finance/accounts/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>حساب جديد</Button>
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (كود / اسم / وصف)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <Select
          value={typeFilter}
          onChange={(e) => setTypeFilter(e.target.value)}
          options={[
            { value: '', label: 'كل الأنواع' },
            ...Object.entries(ACCOUNT_TYPES).map(([k, v]) => ({ value: k, label: v })),
          ]}
          className="w-48"
        />
        <span className="text-sm text-gray-500">{total} نتيجة</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'code',
            header: 'الكود',
            render: (a) => <span className="font-mono text-sm font-semibold">{a.code}</span>,
          },
          {
            key: 'name',
            header: 'اسم الحساب',
            render: (a) => (
              <div>
                <Link href={`/finance/accounts/${a.id}`} className="font-semibold text-gray-800 hover:text-blue-600">
                  {a.name}
                </Link>
                {a.description && <p className="text-xs text-gray-500">{a.description}</p>}
              </div>
            ),
          },
          {
            key: 'type',
            header: 'النوع',
            align: 'center',
            render: (a) => {
              const typeLabel = ACCOUNT_TYPES[a.type] || '—';
              const variant: any =
                a.type === 1 ? 'info' :
                a.type === 2 ? 'warning' :
                a.type === 3 ? 'neutral' :
                a.type === 4 ? 'success' : 'danger';
              return <Badge variant={variant}>{typeLabel}</Badge>;
            },
          },
          {
            key: 'normalBalance',
            header: 'الرصيد الطبيعي',
            align: 'center',
            render: (a) => <span className="text-xs font-mono">{a.normalBalance === 1 ? 'مدين (Dr)' : 'دائن (Cr)'}</span>,
          },
          {
            key: 'isPostable',
            header: 'قابل للترحيل',
            align: 'center',
            render: (a) => a.isPostable ? <Badge variant="success">نعم</Badge> : <Badge variant="neutral">لا (رئيسي)</Badge>,
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (a) => a.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">معطّل</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (a) => (
              <EntityActions
                itemLabel={a.name}
                editHref={`/finance/accounts/${a.id}/edit`}
                onDelete={async () => { await financeApi.deleteAccount(a.id); await load(); }}
              />
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(a) => a.id}
        emptyMessage={search ? 'لا توجد حسابات تطابق البحث.' : 'لا توجد حسابات.'}
      />

      <Pagination
        total={total}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />
    </div>
  );
}
