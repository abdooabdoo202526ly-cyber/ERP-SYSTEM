'use client';

// صفحة الإجازات (Leaves) — قائمة + Approve/Reject

import { useEffect, useState } from 'react';
import { formatDate, formatTime } from '@/lib/utils';
import Link from 'next/link';
import { Plus, Check, X } from 'lucide-react';
import { Button, Table, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  LeaveRequest,
  LEAVE_TYPES,
  LEAVE_STATUSES,
  LEAVE_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';

export default function LeavesPage() {
  const { loading: authLoading, user } = useAuth();
  const [leaves, setLeaves] = useState<LeaveRequest[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionId, setActionId] = useState<string | null>(null);

  // هل المستخدم الحالي مدير (يستطيع Approve)؟
  const isManager = user?.roles?.some((r) => ['Admin', 'HRManager'].includes(r)) ?? false;

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await hrApi.listLeaves();
      setLeaves(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل طلبات الإجازات.'));
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async (id: string) => {
    setActionId(id);
    try {
      await hrApi.approveLeave(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشلت الموافقة.'));
    } finally {
      setActionId(null);
    }
  };

  const handleReject = async (id: string) => {
    setActionId(id);
    try {
      await hrApi.rejectLeave(id);
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الرفض.'));
    } finally {
      setActionId(null);
    }
  };

  return (
    <div>
      <PageHeader
        title="🌴 الإجازات"
        description="طلبات إجازات الموظفين"
        actions={
          <Link href="/hr/leaves/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              طلب إجازة
            </Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      <Table
        columns={[
          {
            key: 'employee',
            header: 'الموظف',
            render: (l) => l.employeeName || <span className="text-gray-400 text-xs">{l.employeeId}</span>,
          },
          {
            key: 'leaveType',
            header: 'النوع',
            render: (l) => <Badge variant="info">{LEAVE_TYPES[l.leaveType] || l.leaveType}</Badge>,
          },
          {
            key: 'period',
            header: 'الفترة',
            render: (l) => (
              <div>
                <p className="text-sm text-gray-800">
                  {formatDate(l.startDate)} - {formatDate(l.endDate)}
                </p>
                <p className="text-xs text-gray-500">{l.totalDays} يوم</p>
              </div>
            ),
          },
          {
            key: 'reason',
            header: 'السبب',
            render: (l) =>
              l.reason ? <span className="text-sm text-gray-700 max-w-[200px] truncate inline-block">{l.reason}</span> : <span className="text-gray-400 text-xs">—</span>,
          },
          {
            key: 'status',
            header: 'الحالة',
            render: (l) => (
              <Badge variant={LEAVE_STATUS_VARIANTS[l.status] || 'neutral'}>
                {LEAVE_STATUSES[l.status] || l.status}
              </Badge>
            ),
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (l) =>
              isManager && l.status === 1 ? (
                <div className="flex items-center gap-1 justify-center">
                  <button
                    onClick={() => handleApprove(l.id)}
                    disabled={actionId === l.id}
                    className="p-1.5 rounded-lg text-green-600 hover:bg-green-50 disabled:opacity-50"
                    title="موافقة"
                    aria-label="موافقة"
                  >
                    <Check className="h-4 w-4" />
                  </button>
                  <button
                    onClick={() => handleReject(l.id)}
                    disabled={actionId === l.id}
                    className="p-1.5 rounded-lg text-red-600 hover:bg-red-50 disabled:opacity-50"
                    title="رفض"
                    aria-label="رفض"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </div>
              ) : (
                <span className="text-xs text-gray-400">—</span>
              ),
          },
        ]}
        data={leaves}
        loading={loading}
        rowKey={(l) => l.id}
        emptyMessage="لا توجد طلبات إجازات."
      />

      {!loading && leaves.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">{leaves.length} طلب</p>
      )}
    </div>
  );
}


