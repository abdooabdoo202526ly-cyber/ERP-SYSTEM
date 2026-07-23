'use client';

// صفحة تفاصيل سجل حضور واحد (Attendance Detail) — Header (Employee, Date, CheckIn/Out,
//   WorkHours, Status) + Edit (نفس اليوم فقط) + Delete (نفس اليوم فقط).

import { useEffect, useState, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Edit3,
  Trash2,
  RefreshCw,
  LogIn,
  LogOut as LogOutIcon,
  Clock,
  User as UserIcon,
  CalendarDays,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  MessageSquare,
  Hash,
  Network,
} from 'lucide-react';
import { Button, Card, Badge, PageHeader, Input } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  AttendanceRecord,
  Employee,
  ATTENDANCE_TYPES,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatTime, formatDateTime } from '@/lib/utils';

// "Late" threshold: إذا الـ CheckIn بعد الساعة 09:00 (UTC), نعتبرها Late.
const LATE_CHECKIN_HOUR = 9;

function deriveStatus(
  rec: AttendanceRecord,
  pairedCheckOut?: AttendanceRecord
): { label: string; variant: 'success' | 'warning' | 'danger' | 'neutral'; icon: React.ReactNode } {
  if (rec.type === 1) {
    // CheckIn
    const tsHour = new Date(rec.timestamp).getUTCHours();
    if (tsHour >= LATE_CHECKIN_HOUR) {
      return { label: 'متأخر (Late)', variant: 'warning', icon: <AlertTriangle className="h-3 w-3" /> };
    }
    return { label: 'حضور (Present)', variant: 'success', icon: <CheckCircle2 className="h-3 w-3" /> };
  }
  // CheckOut
  if (pairedCheckOut) {
    return { label: 'انصراف (Present)', variant: 'success', icon: <CheckCircle2 className="h-3 w-3" /> };
  }
  // CheckOut بدون CheckIn مرتبط — نعتبرها Absent إذا لم نجد CheckIn سابق.
  return { label: 'غياب (Absent)', variant: 'danger', icon: <XCircle className="h-3 w-3" /> };
}

export default function AttendanceDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  useAuth();

  const [record, setRecord] = useState<AttendanceRecord | null>(null);
  const [employee, setEmployee] = useState<Employee | null>(null);
  // الـ CheckIn/CheckOut المقترن على نفس اليوم (للعرض + حساب workHours).
  const [pairedCheckIn, setPairedCheckIn] = useState<AttendanceRecord | undefined>();
  const [pairedCheckOut, setPairedCheckOut] = useState<AttendanceRecord | undefined>();

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<'edit' | 'delete' | 'refresh' | null>(null);

  // حالة التعديل (inline) — فقط إذا نفس اليوم
  const [isEditingNotes, setIsEditingNotes] = useState(false);
  const [notesDraft, setNotesDraft] = useState('');

  const load = useCallback(async () => {
    if (!params.id) return;
    setLoading(true);
    setError(null);
    try {
      const rec = await hrApi.getAttendance(params.id);
      setRecord(rec);
      setNotesDraft(rec.notes || '');

      // employee name lookup (best-effort)
      try {
        const emps = await hrApi.listEmployees();
        setEmployee(emps.find((e) => e.id === rec.employeeId) || null);
      } catch {
        setEmployee(null);
      }

      // الـ CheckIn/CheckOut المقترن على نفس اليوم (للحساب).
      try {
        const day = new Date(rec.timestamp);
        const dayStart = new Date(day);
        dayStart.setUTCHours(0, 0, 0, 0);
        const dayEnd = new Date(day);
        dayEnd.setUTCHours(23, 59, 59, 999);

        const list = await hrApi.listAttendance({
          employeeId: rec.employeeId,
          from: dayStart.toISOString(),
          to: dayEnd.toISOString(),
        });
        const sorted = list.sort(
          (a, b) => new Date(a.timestamp).getTime() - new Date(b.timestamp).getTime()
        );
        if (rec.type === 1) {
          // CheckIn: ابحث عن CheckOut لاحق
          setPairedCheckIn(rec);
          const later = sorted.find((r) => r.type === 2 && r.id !== rec.id);
          setPairedCheckOut(later);
        } else {
          // CheckOut: ابحث عن CheckIn سابق
          setPairedCheckOut(rec);
          const earlier = [...sorted].reverse().find((r) => r.type === 1 && r.id !== rec.id);
          setPairedCheckIn(earlier);
        }
      } catch {
        setPairedCheckIn(undefined);
        setPairedCheckOut(undefined);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل سجل الحضور.'));
    } finally {
      setLoading(false);
    }
  }, [params.id]);

  useEffect(() => {
    load();
  }, [load]);

  // هل السجل في نفس اليوم؟ (نفس اليوم = يمكن تعديل/حذف)
  const isSameDay = (() => {
    if (!record) return false;
    const recDate = new Date(record.timestamp);
    const today = new Date();
    return (
      recDate.getUTCFullYear() === today.getUTCFullYear() &&
      recDate.getUTCMonth() === today.getUTCMonth() &&
      recDate.getUTCDate() === today.getUTCDate()
    );
  })();

  const onSaveNotes = async () => {
    if (!record) return;
    setActionLoading('edit');
    try {
      const updated = await hrApi.updateAttendance(record.id, { notes: notesDraft });
      setRecord(updated);
      setIsEditingNotes(false);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديث الملاحظات.'));
    } finally {
      setActionLoading(null);
    }
  };

  const onDelete = async () => {
    if (!record) return;
    if (!confirm('حذف سجل الحضور نهائياً؟ هذا الإجراء لا يمكن التراجع عنه.')) return;
    setActionLoading('delete');
    try {
      await hrApi.deleteAttendance(record.id);
      router.push('/hr/attendance');
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل حذف السجل.'));
      setActionLoading(null);
    }
  };

  // حساب ساعات العمل
  const workHours = (() => {
    if (!record || !pairedCheckIn || !pairedCheckOut) return null;
    const inTs = new Date(pairedCheckIn.timestamp).getTime();
    const outTs = new Date(pairedCheckOut.timestamp).getTime();
    if (isNaN(inTs) || isNaN(outTs) || outTs < inTs) return null;
    const ms = outTs - inTs;
    const hours = Math.floor(ms / (1000 * 60 * 60));
    const minutes = Math.floor((ms % (1000 * 60 * 60)) / (1000 * 60));
    return { hours, minutes, totalMs: ms };
  })();

  if (loading) {
    return (
      <div>
        <PageHeader title="🕐 سجل حضور" description="جاري التحميل..." />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!record) {
    return (
      <div>
        <PageHeader
          title="السجل غير موجود"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الحضور', href: '/hr/attendance' },
            { label: 'غير موجود' },
          ]}
          actions={
            <Link href="/hr/attendance">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
          {error || 'لم يتم العثور على السجل.'}
        </div>
      </div>
    );
  }

  const status = deriveStatus(record, pairedCheckOut);

  return (
    <div>
      <PageHeader
        title="🕐 سجل حضور"
        description={`${employee?.fullName || record.employeeName || record.employeeId} • ${formatDate(record.timestamp)}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الحضور', href: '/hr/attendance' },
          { label: formatDate(record.timestamp) },
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
            {isSameDay && (
              <>
                <Button
                  variant="primary"
                  size="sm"
                  onClick={() => setIsEditingNotes((v) => !v)}
                  iconLeft={<Edit3 className="h-4 w-4" />}
                  disabled={isEditingNotes}
                >
                  تعديل
                </Button>
                <Button
                  variant="danger"
                  size="sm"
                  onClick={onDelete}
                  loading={actionLoading === 'delete'}
                  iconLeft={<Trash2 className="h-4 w-4" />}
                >
                  حذف
                </Button>
              </>
            )}
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {!isSameDay && (
        <div className="mb-4 bg-yellow-50 border border-yellow-200 text-yellow-800 px-4 py-2 rounded-lg text-xs flex items-center gap-2">
          <AlertTriangle className="h-4 w-4" />
          هذا السجل من يوم سابق — لا يمكن تعديله أو حذفه.
        </div>
      )}

      {/* Header card: الحالة + type + employee + actions */}
      <Card className="mb-6" accent="blue">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <p className="text-xs text-gray-500 mb-1">الحالة</p>
            <Badge variant={status.variant} size="md">
              <span className="inline-flex items-center gap-1">
                {status.icon} {status.label}
              </span>
            </Badge>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">النوع</p>
            {record.type === 1 ? (
              <Badge variant="success" size="md">
                <LogIn className="h-3 w-3 ml-1" /> {ATTENDANCE_TYPES[1]}
              </Badge>
            ) : (
              <Badge variant="danger" size="md">
                <LogOutIcon className="h-3 w-3 ml-1" /> {ATTENDANCE_TYPES[2]}
              </Badge>
            )}
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">التاريخ</p>
            <p className="font-semibold text-gray-800">{formatDate(record.timestamp)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">الوقت</p>
            <p className="font-mono font-bold text-gray-800 text-lg">{formatTime(record.timestamp)}</p>
          </div>
        </div>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Employee + Work Hours */}
        <Card title="الموظف وساعات العمل" accent="green" className="lg:col-span-2">
          <div className="space-y-4 text-sm">
            <div className="flex items-start gap-2">
              <UserIcon className="h-4 w-4 text-gray-400 mt-0.5" />
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500">الموظف</p>
                {employee ? (
                  <Link
                    href={`/hr/employees/${employee.id}`}
                    className="text-blue-600 hover:underline font-semibold"
                  >
                    {employee.fullName}
                  </Link>
                ) : (
                  <span className="text-gray-700">{record.employeeName || record.employeeId}</span>
                )}
                {employee?.employeeNumber && (
                  <p className="text-xs text-gray-500 font-mono mt-0.5">{employee.employeeNumber}</p>
                )}
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-3 pt-3 border-t border-gray-100">
              <div className="p-3 rounded-lg bg-green-50">
                <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                  <LogIn className="h-3 w-3" /> CheckIn
                </p>
                {pairedCheckIn ? (
                  <div>
                    <p className="font-mono font-bold text-green-700">
                      {formatTime(pairedCheckIn.timestamp)}
                    </p>
                    <p className="text-xs text-gray-500 font-mono">{formatDate(pairedCheckIn.timestamp)}</p>
                  </div>
                ) : (
                  <span className="text-gray-400 text-sm">— لا يوجد —</span>
                )}
              </div>
              <div className="p-3 rounded-lg bg-red-50">
                <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                  <LogOutIcon className="h-3 w-3" /> CheckOut
                </p>
                {pairedCheckOut ? (
                  <div>
                    <p className="font-mono font-bold text-red-700">
                      {formatTime(pairedCheckOut.timestamp)}
                    </p>
                    <p className="text-xs text-gray-500 font-mono">{formatDate(pairedCheckOut.timestamp)}</p>
                  </div>
                ) : (
                  <span className="text-gray-400 text-sm">— لا يوجد —</span>
                )}
              </div>
              <div className="p-3 rounded-lg bg-blue-50">
                <p className="text-xs text-gray-500 mb-1 flex items-center gap-1">
                  <Clock className="h-3 w-3" /> Work Hours
                </p>
                {workHours ? (
                  <p className="font-mono font-bold text-blue-700">
                    {workHours.hours}س {workHours.minutes}د
                  </p>
                ) : (
                  <span className="text-gray-400 text-sm">— غير متاح —</span>
                )}
              </div>
            </div>
          </div>
        </Card>

        {/* Technical / meta */}
        <Card title="معلومات تقنية" accent="gray">
          <div className="space-y-3 text-sm">
            <div className="flex items-start gap-2">
              <Hash className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">السجل ID</p>
                <p className="font-mono text-xs">{record.id}</p>
              </div>
            </div>
            <div className="flex items-start gap-2">
              <CalendarDays className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">Timestamp كامل</p>
                <p className="font-mono text-xs">{formatDateTime(record.timestamp)}</p>
              </div>
            </div>
            {record.ipAddress && (
              <div className="flex items-start gap-2">
                <Network className="h-4 w-4 text-gray-400 mt-0.5" />
                <div>
                  <p className="text-xs text-gray-500">IP Address</p>
                  <p className="font-mono text-xs" dir="ltr">{record.ipAddress}</p>
                </div>
              </div>
            )}
            {record.createdAt && (
              <div className="flex items-start gap-2">
                <Clock className="h-4 w-4 text-gray-400 mt-0.5" />
                <div>
                  <p className="text-xs text-gray-500">تاريخ الإنشاء</p>
                  <p className="font-mono text-xs">{formatDateTime(record.createdAt)}</p>
                </div>
              </div>
            )}
          </div>
        </Card>
      </div>

      {/* Notes (read or edit) */}
      <Card
        className="mt-4"
        title={
          <span className="flex items-center gap-2">
            <MessageSquare className="h-4 w-4 text-gray-500" /> ملاحظات
          </span>
        }
        actions={
          isSameDay && !isEditingNotes ? (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                setNotesDraft(record.notes || '');
                setIsEditingNotes(true);
              }}
              iconLeft={<Edit3 className="h-4 w-4" />}
            >
              تعديل
            </Button>
          ) : null
        }
      >
        {isEditingNotes ? (
          <div className="space-y-3">
            <Input
              label="ملاحظات"
              value={notesDraft}
              onChange={(e) => setNotesDraft(e.target.value)}
              placeholder="ملاحظات اختيارية"
              hint="التعديل مسموح فقط في نفس اليوم."
            />
            <div className="flex items-center gap-2">
              <Button
                variant="primary"
                size="sm"
                onClick={onSaveNotes}
                loading={actionLoading === 'edit'}
                iconLeft={<Edit3 className="h-4 w-4" />}
              >
                حفظ
              </Button>
              <Button
                variant="ghost"
                size="sm"
                onClick={() => {
                  setNotesDraft(record.notes || '');
                  setIsEditingNotes(false);
                }}
                disabled={actionLoading === 'edit'}
              >
                إلغاء
              </Button>
            </div>
          </div>
        ) : record.notes ? (
          <p className="text-sm text-gray-800 whitespace-pre-wrap">{record.notes}</p>
        ) : (
          <p className="text-sm text-gray-400">— لا توجد ملاحظات —</p>
        )}
      </Card>

      <div className="mt-6">
        <Link href="/hr/attendance">
          <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
            رجوع للحضور
          </Button>
        </Link>
      </div>
    </div>
  );
}
