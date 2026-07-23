'use client';

// صفحة قائمة الموظفين (Employees) — v1.0.32: pagination + search + status filter

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus, Mail, Phone } from 'lucide-react';
import { Button, Table, Badge, PageHeader, EntityActions, SearchBar, Pagination, Select } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { hrApi, Employee, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function EmployeesPage() {
  const { loading: authLoading } = useAuth();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // v1.0.32: filters + pagination
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'inactive'>('all');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  useEffect(() => { setPage(1); }, [search, statusFilter]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await hrApi.listEmployees();
      setEmployees(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الموظفين.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    return employees.filter((e) => {
      if (statusFilter === 'active' && !e.isActive) return false;
      if (statusFilter === 'inactive' && e.isActive) return false;
      if (!q) return true;
      return (
        (e.employeeNumber || '').toLowerCase().includes(q) ||
        (e.fullName || '').toLowerCase().includes(q) ||
        (e.email || '').toLowerCase().includes(q) ||
        (e.phone || '').toLowerCase().includes(q) ||
        (e.nationalId || '').toLowerCase().includes(q) ||
        (e.departmentName || '').toLowerCase().includes(q) ||
        (e.jobTitle || '').toLowerCase().includes(q)
      );
    });
  }, [employees, search, statusFilter]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  return (
    <div>
      <PageHeader
        title="👨‍💼 الموظفون"
        description="قائمة موظفي الشركة"
        actions={
          <Link href="/hr/employees/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>موظف جديد</Button>
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (رقم / اسم / بريد / قسم / وظيفة)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <Select
          value={statusFilter}
          onChange={(e) => setStatusFilter(e.target.value as 'all' | 'active' | 'inactive')}
          options={[
            { value: 'all', label: 'الكل' },
            { value: 'active', label: 'نشط فقط' },
            { value: 'inactive', label: 'منتهية خدمته' },
          ]}
          className="w-44"
        />
        <span className="text-sm text-gray-500">{total} نتيجة</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'employeeNumber',
            header: 'الرقم الوظيفي',
            render: (e) => <span className="font-mono text-sm">{e.employeeNumber}</span>,
          },
          {
            key: 'fullName',
            header: 'الاسم الكامل',
            render: (e) => (
              <div>
                <Link href={`/hr/employees/${e.id}`} className="font-semibold text-gray-800 hover:text-blue-600">
                  {e.fullName}
                </Link>
                {e.jobTitle && <p className="text-xs text-gray-500">{e.jobTitle}</p>}
              </div>
            ),
          },
          {
            key: 'department',
            header: 'القسم',
            render: (e) => e.departmentName || <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'contact',
            header: 'الاتصال',
            render: (e) => (
              <div className="text-xs space-y-0.5">
                {e.email && <p className="text-gray-600 flex items-center gap-1"><Mail className="h-3 w-3" />{e.email}</p>}
                {e.phone && <p className="text-gray-600 flex items-center gap-1"><Phone className="h-3 w-3" />{e.phone}</p>}
              </div>
            ),
          },
          {
            key: 'hireDate',
            header: 'تاريخ التعيين',
            render: (e) => <span className="text-sm">{formatDate(e.hireDate)}</span>,
          },
          {
            key: 'baseSalary',
            header: 'الراتب الأساسي',
            align: 'end',
            render: (e) => <span className="font-mono text-sm">{formatNumber(e.baseSalary || 0)} LYD</span>,
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (e) => e.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">منتهية خدمته</Badge>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (e) => (
              <EntityActions
                itemLabel={e.fullName}
                editHref={`/hr/employees/${e.id}/edit`}
                onDelete={async () => { await hrApi.deactivateEmployee(e.id); await load(); }}
              />
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(e) => e.id}
        emptyMessage={search ? 'لا توجد نتائج تطابق البحث.' : 'لا يوجد موظفون.'}
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
