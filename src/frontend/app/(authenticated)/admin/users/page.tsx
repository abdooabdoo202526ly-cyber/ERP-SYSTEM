'use client';

// v1.0.33: إدارة المستخدمين (Users) + ربط الأدوار (Roles)

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { UserCog, Mail, Shield, X, Plus } from 'lucide-react';
import {
  Button, Table, Badge, PageHeader, Modal, SearchBar, Pagination,
} from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { identityApi, Role, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';

interface UserWithRoles {
  id: string;
  email: string;
  fullName: string;
  isActive: boolean;
  lastLoginAt?: string;
  createdAt: string;
  roles: string[];
}

export default function UsersPage() {
  const { loading: authLoading } = useAuth();
  const [users, setUsers] = useState<UserWithRoles[]>([]);
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  // manage roles modal
  const [manageRolesUser, setManageRolesUser] = useState<UserWithRoles | null>(null);
  const [savingRole, setSavingRole] = useState(false);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  useEffect(() => { setPage(1); }, [search]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [u, r] = await Promise.all([
        identityApi.listUsers(),
        identityApi.listRoles(),
      ]);
      setUsers(u as any);
      setRoles(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل المستخدمين. تأكد من صلاحية Admin.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return users;
    return users.filter((u) =>
      u.email.toLowerCase().includes(q) ||
      u.fullName.toLowerCase().includes(q) ||
      u.roles.some((r) => r.toLowerCase().includes(q))
    );
  }, [users, search]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  const toggleRole = async (user: UserWithRoles, role: Role) => {
    setSavingRole(true);
    try {
      const has = user.roles.includes(role.name);
      if (has) {
        await identityApi.removeRole(user.id, role.id);
      } else {
        await identityApi.assignRole(user.id, role.id);
      }
      // Reload
      const updated = await identityApi.listUsers();
      setUsers(updated as any);
      if (manageRolesUser?.id === user.id) {
        const u = (updated as any[]).find((x) => x.id === user.id);
        if (u) setManageRolesUser(u);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تعديل الدور.'));
    } finally {
      setSavingRole(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="👥 المستخدمون (Users)"
        description="قائمة المستخدمين وإدارة أدوارهم"
        actions={
          <Link href="/admin/roles">
            <Button variant="ghost" iconLeft={<Shield className="h-4 w-4" />}>
              الأدوار
            </Button>
          </Link>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (اسم / بريد / دور)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <span className="text-sm text-gray-500">{total} مستخدم</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'fullName',
            header: 'الاسم الكامل',
            render: (u: UserWithRoles) => (
              <div>
                <div className="font-semibold text-gray-800">{u.fullName}</div>
                <div className="text-xs text-gray-500 flex items-center gap-1">
                  <Mail className="h-3 w-3" />{u.email}
                </div>
              </div>
            ),
          },
          {
            key: 'isActive',
            header: 'الحالة',
            align: 'center',
            render: (u: UserWithRoles) => u.isActive ? <Badge variant="success">نشط</Badge> : <Badge variant="neutral">معطّل</Badge>,
          },
          {
            key: 'roles',
            header: 'الأدوار',
            render: (u: UserWithRoles) => (
              <div className="flex flex-wrap gap-1">
                {u.roles.length === 0 ? (
                  <span className="text-xs text-gray-400">بدون أدوار</span>
                ) : (
                  u.roles.map((r) => (
                    <Badge key={r} variant="info">{r}</Badge>
                  ))
                )}
              </div>
            ),
          },
          {
            key: 'lastLoginAt',
            header: 'آخر دخول',
            render: (u: UserWithRoles) => u.lastLoginAt ? <span className="text-xs text-gray-500">{formatDate(u.lastLoginAt)}</span> : <span className="text-xs text-gray-400">لم يدخل</span>,
          },
          {
            key: 'createdAt',
            header: 'تاريخ الإنشاء',
            render: (u: UserWithRoles) => <span className="text-xs text-gray-500">{formatDate(u.createdAt)}</span>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (u: UserWithRoles) => (
              <Button
                size="sm"
                variant="secondary"
                iconLeft={<Shield className="h-3.5 w-3.5" />}
                onClick={() => setManageRolesUser(u)}
              >
                إدارة الأدوار
              </Button>
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(u: UserWithRoles) => u.id}
        emptyMessage={search ? 'لا توجد نتائج.' : 'لا يوجد مستخدمون.'}
      />

      <Pagination
        total={total}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />

      {/* Manage Roles Modal */}
      <Modal
        open={!!manageRolesUser}
        onClose={() => setManageRolesUser(null)}
        title={manageRolesUser ? `إدارة أدوار: ${manageRolesUser.fullName}` : ''}
        size="md"
        footer={
          <div className="flex justify-end">
            <Button variant="ghost" onClick={() => setManageRolesUser(null)}>إغلاق</Button>
          </div>
        }
      >
        {manageRolesUser && (
          <div className="space-y-2">
            <p className="text-sm text-gray-600 mb-3">
              <Mail className="h-3.5 w-3.5 inline" /> {manageRolesUser.email}
            </p>
            {roles.length === 0 ? (
              <p className="text-sm text-gray-500">لا توجد أدوار. أنشئ أدوار أولاً من صفحة الأدوار.</p>
            ) : (
              roles.map((role) => {
                const has = manageRolesUser.roles.includes(role.name);
                return (
                  <div
                    key={role.id}
                    className={`flex items-center justify-between p-3 rounded-lg border ${
                      has ? 'border-emerald-300 bg-emerald-50' : 'border-gray-200 bg-white'
                    }`}
                  >
                    <div className="flex-1">
                      <div className="flex items-center gap-2">
                        <Shield className={`h-4 w-4 ${has ? 'text-emerald-600' : 'text-gray-400'}`} />
                        <span className="font-semibold">{role.name}</span>
                        <Badge variant={has ? 'success' : 'neutral'}>{has ? 'مفعّل' : 'غير مفعّل'}</Badge>
                      </div>
                      {role.description && <p className="text-xs text-gray-500 mt-1 ms-6">{role.description}</p>}
                    </div>
                    <Button
                      size="sm"
                      variant={has ? 'ghost' : 'primary'}
                      loading={savingRole}
                      onClick={() => toggleRole(manageRolesUser, role)}
                    >
                      {has ? 'إلغاء' : 'تفعيل'}
                    </Button>
                  </div>
                );
              })
            )}
          </div>
        )}
      </Modal>
    </div>
  );
}
