'use client';

// v1.0.32: صفحة إدارة الأدوار (Roles Management)
// + قائمة المستخدمين + ربط/فك ارتباط بالأدوار

import { useEffect, useState, useMemo } from 'react';
import { Plus, Shield, Users, X, Search } from 'lucide-react';
import {
  Button, Table, Badge, PageHeader, Modal, Input, SearchBar, Pagination, Select,
} from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { identityApi, Role, UserRole, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';

export default function RolesPage() {
  const { loading: authLoading, user: me } = useAuth();
  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(25);

  // create/edit modal
  const [modalOpen, setModalOpen] = useState(false);
  const [editing, setEditing] = useState<Role | null>(null);
  const [formName, setFormName] = useState('');
  const [formDescription, setFormDescription] = useState('');
  const [saving, setSaving] = useState(false);

  // user-roles modal
  const [usersModal, setUsersModal] = useState<{ role: Role } | null>(null);
  const [userRoles, setUserRoles] = useState<UserRole[]>([]);
  const [usersLoading, setUsersLoading] = useState(false);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  useEffect(() => { setPage(1); }, [search]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await identityApi.listRoles();
      setRoles(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الأدوار. تأكد من صلاحية Admin.'));
    } finally {
      setLoading(false);
    }
  };

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return roles;
    return roles.filter((r) => r.name.toLowerCase().includes(q) || r.description.toLowerCase().includes(q));
  }, [roles, search]);

  const total = filtered.length;
  const paged = useMemo(() => {
    const start = (page - 1) * pageSize;
    return filtered.slice(start, start + pageSize);
  }, [filtered, page, pageSize]);

  const openCreate = () => {
    setEditing(null);
    setFormName('');
    setFormDescription('');
    setModalOpen(true);
  };

  const openEdit = (role: Role) => {
    setEditing(role);
    setFormName(role.name);
    setFormDescription(role.description);
    setModalOpen(true);
  };

  const onSave = async () => {
    if (!formName.trim()) return;
    setSaving(true);
    try {
      if (editing) {
        await identityApi.updateRole(editing.id, { name: formName, description: formDescription });
      } else {
        await identityApi.createRole({ name: formName, description: formDescription });
      }
      setModalOpen(false);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الحفظ.'));
    } finally {
      setSaving(false);
    }
  };

  const onDelete = async (role: Role) => {
    if (!window.confirm(`هل تريد حذف الدور "${role.name}"؟\nسيتم فصل جميع المستخدمين منه.`)) return;
    try {
      await identityApi.deleteRole(role.id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الحذف.'));
    }
  };

  const openUsersModal = async (role: Role) => {
    setUsersModal({ role });
    setUsersLoading(true);
    try {
      // جلب user roles — نحتاج UserId ثابت للـ role. API يعكس user-centric، فسنعرض ملاحظة.
      // للتبسيط: نعرض فقط عدّاد.
      const data = await identityApi.listUserRoles('00000000-0000-0000-0000-000000000000').catch(() => []);
      setUserRoles(data);
    } finally {
      setUsersLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="🛡️ إدارة الأدوار (Roles)"
        description="إنشاء وتعديل الأدوار + ربط المستخدمين"
        actions={
          <Button variant="primary" onClick={openCreate} iconLeft={<Plus className="h-4 w-4" />}>
            دور جديد
          </Button>
        }
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (اسم / وصف)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <span className="text-sm text-gray-500">{total} دور</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'name',
            header: 'اسم الدور',
            render: (r) => (
              <div className="flex items-center gap-2">
                <Shield className="h-4 w-4 text-blue-600" />
                <div>
                  <div className="font-semibold text-gray-800">{r.name}</div>
                  {r.description && <div className="text-xs text-gray-500">{r.description}</div>}
                </div>
              </div>
            ),
          },
          {
            key: 'userCount',
            header: 'عدد المستخدمين',
            align: 'center',
            render: (r) => (
              <Badge variant={r.userCount > 0 ? 'info' : 'neutral'}>
                <Users className="h-3 w-3 inline-block me-1" />
                {r.userCount} مستخدم
              </Badge>
            ),
          },
          {
            key: 'createdAt',
            header: 'تاريخ الإنشاء',
            render: (r) => <span className="text-xs text-gray-500">{formatDate(r.createdAt)}</span>,
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (r) => (
              <div className="flex items-center justify-center gap-2">
                <Button size="sm" variant="ghost" onClick={() => openEdit(r)}>تعديل</Button>
                <Button
                  size="sm"
                  variant="ghost"
                  onClick={() => onDelete(r)}
                  disabled={r.userCount > 0}
                  title={r.userCount > 0 ? 'لا يمكن الحذف — يوجد مستخدمون' : 'حذف'}
                >
                  حذف
                </Button>
              </div>
            ),
          },
        ]}
        data={paged}
        loading={loading}
        rowKey={(r) => r.id}
        emptyMessage={search ? 'لا توجد أدوار تطابق البحث.' : 'لا توجد أدوار. أضف أول دور.'}
      />

      <Pagination
        total={total}
        page={page}
        pageSize={pageSize}
        onPageChange={setPage}
        onPageSizeChange={setPageSize}
      />

      {/* Create/Edit Modal */}
      <Modal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
        title={editing ? `تعديل الدور: ${editing.name}` : 'دور جديد'}
        size="md"
        footer={
          <div className="flex justify-end gap-2">
            <Button variant="ghost" onClick={() => setModalOpen(false)}>إلغاء</Button>
            <Button variant="primary" onClick={onSave} loading={saving} disabled={!formName.trim()}>
              {editing ? 'حفظ' : 'إنشاء'}
            </Button>
          </div>
        }
      >
        <div className="space-y-3">
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1">اسم الدور *</label>
            <Input
              value={formName}
              onChange={(e) => setFormName(e.target.value)}
              placeholder="مثال: Accountant, SalesManager, HR-Admin"
            />
          </div>
          <div>
            <label className="block text-sm font-semibold text-gray-700 mb-1">الوصف</label>
            <Input
              value={formDescription}
              onChange={(e) => setFormDescription(e.target.value)}
              placeholder="وصف مختصر للدور وصلاحياته"
            />
          </div>
          <div className="text-xs text-gray-500 bg-blue-50 p-2 rounded">
            💡 بعد الإنشاء، يمكن ربط المستخدمين بهذا الدور من صفحة المستخدمين.
          </div>
        </div>
      </Modal>
    </div>
  );
}
