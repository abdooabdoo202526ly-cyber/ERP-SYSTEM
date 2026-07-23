'use client';

// صفحة تفاصيل طلب إجازة (Leave Detail) — Header + Status + Approver + workflow (Approve/Reject)
// + أزرار Edit / Back. التعديل متاح فقط إذا الحالة Pending.

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Edit3,
  Check,
  X,
  RefreshCw,
  User as UserIcon,
  CalendarRange,
  Hash,
  MessageSquare,
  UserCheck,
  Clock,
  CalendarOff,
} from 'lucide-react';
import { Button, Card, Badge, PageHeader } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  LeaveRequest,
  Employee,
  LEAVE_TYPES,
  LEAVE_STATUSES,
  LEAVE_STATUS_VARIANTS,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime } from '@/lib/utils';

export default function LeaveDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const { user } = useAuth();

  const [leave, setLeave] = useState<LeaveRequest | null>(null);
  const [employee, setEmployee] = useState<Employee | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<'approve' | 'reject' | 'refresh' | null>(null);

  // هل المستخدم الحالي يستطيع Approve/Reject؟ (Admin أو HRManager)
  const canApprove =
    user?.roles?.some((r) => ['Admin', 'HRManager'].includes(r)) ?? false;

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const l = await hrApi.getLeave(params.id);
      setLeave(l);

      // employee name lookup (best-effort)
      try {
        const emps = await hrApi.listEmployees();
        const e = emps.find((x) => x.id === l.employeeId) || null;
        setEmployee(e);
      } catch {
        setEmployee(null);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل طلب الإجازة.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  const onApprove = async () => {
    if (!leave) return;
    if (!confirm('هل تريد الموافقة على هذا الطلب؟')) return;
    setActionLoading('approve');
    try {
      const updated = await hrApi.approveLeave(leave.id);
      setLeave(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشلت الموافقة.'));
    } finally {
      setActionLoading(null);
    }
  };

  const onReject = async () => {
    if (!leave) return;
    if (!confirm('هل تريد رفض هذا الطلب؟')) return;
    setActionLoading('reject');
    try {
      const updated = await hrApi.rejectLeave(leave.id);
      setLeave(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الرفض.'));
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="🌴 طلب إجازة" description="جاري التحميل..." />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!leave) {
    return (
      <div>
        <PageHeader
          title="الطلب غير موجود"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الإجازات', href: '/hr/leaves' },
            { label: 'غير موجود' },
          ]}
          actions={
            <Link href="/hr/leaves">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
          {error || 'لم يتم العثور على الطلب.'}
        </div>
      </div>
    );
  }

  const isPending = leave.status === 1; // Pending
  const isApproved = leave.status === 2;
  const isRejected = leave.status === 3;

  return (
    <div>
      <PageHeader
        title="🌴 طلب إجازة"
        description={`${employee?.fullName || leave.employeeName || leave.employeeId} • ${LEAVE_TYPES[leave.leaveType] || leave.leaveType}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الإجازات', href: '/hr/leaves' },
          { label: `${employee?.fullName || 'طلب'} — ${formatDate(leave.startDate)}` },
        ]}
        actions={
          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => {
                setActionLoading('refresh');
                load().finally(() => setActionLoading(null));
              }}
              loading={actionLoading === 'refresh'}
              iconLeft={<RefreshCw className="h-4 w-4" />}
            >
              تحديث
            </Button>
            {isPending && (
              <Link href={`/hr/leaves/${leave.id}/edit`}>
                <Button variant="primary" size="sm" iconLeft={<Edit3 className="h-4 w-4" />}>
                  تعديل
                </Button>
              </Link>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {/* Status header */}
      <Card className="mb-6" accent="blue">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <p className="text-xs text-gray-500 mb-1">الحالة</p>
            <Badge variant={LEAVE_STATUS_VARIANTS[leave.status] || 'neutral'} size="md">
              {LEAVE_STATUSES[leave.status] || leave.status}
            </Badge>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">النوع</p>
            <Badge variant="info" size="md">{LEAVE_TYPES[leave.leaveType] || leave.leaveType}</Badge>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">عدد الأيام</p>
            <p className="font-bold text-gray-800 text-lg">{leave.totalDays} يوم</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">تاريخ التقديم</p>
            <p className="text-sm font-mono text-gray-700">{formatDate(leave.createdAt)}</p>
          </div>
        </div>

        {/* Workflow: Approve/Reject (gated) */}
        {isPending && canApprove && (
          <div className="flex items-center gap-2 mt-4 pt-4 border-t border-gray-100">
            <span className="text-xs text-gray-500 me-2">الإجراءات:</span>
            <Button
              variant="primary"
              size="sm"
              onClick={onApprove}
              loading={actionLoading === 'approve'}
              iconLeft={<Check className="h-4 w-4" />}
            >
              موافقة
            </Button>
            <Button
              variant="danger"
              size="sm"
              onClick={onReject}
              loading={actionLoading === 'reject'}
              iconLeft={<X className="h-4 w-4" />}
            >
              رفض
            </Button>
          </div>
        )}
        {isPending && !canApprove && (
          <div className="mt-4 pt-4 border-t border-gray-100 text-xs text-gray-500 flex items-center gap-2">
            <Clock className="h-4 w-4" />
            <span>الطلب بانتظار موافقة مدير النظام أو مدير الموارد البشرية.</span>
          </div>
        )}
        {isApproved && (
          <div className="mt-4 pt-4 border-t border-gray-100 bg-green-50 -mx-5 -mb-5 px-5 py-3 rounded-b-xl flex items-center gap-2 text-green-800">
            <Check className="h-4 w-4" />
            <span className="text-sm font-semibold">تمت الموافقة على هذا الطلب</span>
          </div>
        )}
        {isRejected && (
          <div className="mt-4 pt-4 border-t border-gray-100 bg-red-50 -mx-5 -mb-5 px-5 py-3 rounded-b-xl flex items-center gap-2 text-red-800">
            <X className="h-4 w-4" />
            <span className="text-sm font-semibold">تم رفض هذا الطلب</span>
          </div>
        )}
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Employee + Period */}
        <Card title="الموظف والفترة" accent="blue" className="lg:col-span-2">
          <div className="space-y-4 text-sm">
            <div className="flex items-start gap-2">
              <UserIcon className="h-4 w-4 text-gray-400 mt-0.5" />
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500">الموظف</p>
                {employee ? (
                  <Link href={`/hr/employees/${employee.id}`} className="text-blue-600 hover:underline font-semibold">
                    {employee.fullName}
                  </Link>
                ) : (
                  <span className="text-gray-700">{leave.employeeName || leave.employeeId}</span>
                )}
                {employee?.employeeNumber && (
                  <p className="text-xs text-gray-500 font-mono mt-0.5">{employee.employeeNumber}</p>
                )}
              </div>
            </div>

            <div className="flex items-start gap-2">
              <CalendarRange className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">الفترة</p>
                <p className="font-semibold text-gray-800">
                  {formatDate(leave.startDate)} ← {formatDate(leave.endDate)}
                </p>
                <p className="text-xs text-gray-500 mt-0.5">
                  <Hash className="h-3 w-3 inline-block" /> {leave.totalDays} يوم
                </p>
              </div>
            </div>

            <div className="flex items-start gap-2">
              <MessageSquare className="h-4 w-4 text-gray-400 mt-0.5" />
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500">السبب</p>
                {leave.reason ? (
                  <p className="text-gray-800 whitespace-pre-wrap">{leave.reason}</p>
                ) : (
                  <span className="text-gray-400">— لم يُذكر سبب —</span>
                )}
              </div>
            </div>

            {leave.notes && (
              <div className="flex items-start gap-2">
                <MessageSquare className="h-4 w-4 text-gray-400 mt-0.5" />
                <div className="min-w-0 flex-1">
                  <p className="text-xs text-gray-500">ملاحظات</p>
                  <p className="text-gray-800 whitespace-pre-wrap">{leave.notes}</p>
                </div>
              </div>
            )}
          </div>
        </Card>

        {/* Approver */}
        <Card title="الموافق والقرار" accent="green">
          <div className="space-y-3 text-sm">
            <div className="flex items-start gap-2">
              <UserCheck className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">المُوافق</p>
                {leave.approverId ? (
                  <span className="font-mono text-xs">{leave.approverId.slice(0, 8)}...</span>
                ) : (
                  <span className="text-gray-400">— بانتظار القرار —</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <Clock className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">تاريخ القرار</p>
                {leave.approvedAt ? (
                  <p className="font-mono text-gray-800">{formatDateTime(leave.approvedAt)}</p>
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <CalendarOff className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">المعرّف</p>
                <p className="font-mono text-[10px] text-gray-700">{leave.id}</p>
              </div>
            </div>
          </div>
        </Card>
      </div>

      {/* Footer actions */}
      <div className="mt-6 flex items-center gap-2">
        <Link href="/hr/leaves">
          <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
            رجوع للقائمة
          </Button>
        </Link>
        {isPending && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => router.push(`/hr/leaves/${leave.id}/edit`)}
            iconLeft={<Edit3 className="h-4 w-4" />}
          >
            تعديل الطلب
          </Button>
        )}
      </div>
    </div>
  );
}
