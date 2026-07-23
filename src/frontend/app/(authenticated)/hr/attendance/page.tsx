'use client';

// صفحة الحضور (Attendance) — CheckIn / CheckOut + history

import { useEffect, useState } from 'react';
import { formatDate, formatTime } from '@/lib/utils';
import { LogIn, LogOut as LogOutIcon, CheckCircle2, Clock } from 'lucide-react';
import { Button, Select, Table, Badge, Card, PageHeader, EntityActions } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  hrApi,
  Employee,
  AttendanceRecord,
  ATTENDANCE_TYPES,
  getErrorMessage,
} from '@/lib/api';

export default function AttendancePage() {
  const { user } = useAuth();
  const [employees, setEmployees] = useState<Employee[]>([]);
  const [selectedEmployee, setSelectedEmployee] = useState('');
  const [history, setHistory] = useState<AttendanceRecord[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(false);
  const [actionLoading, setActionLoading] = useState<1 | 2 | null>(null);
  const [message, setMessage] = useState<{ type: 'success' | 'error'; text: string } | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    loadEmployees();
  }, []);

  useEffect(() => {
    if (selectedEmployee) loadHistory();
  }, [selectedEmployee]);

  const loadEmployees = async () => {
    try {
      const data = await hrApi.listEmployees();
      setEmployees(data);
      // حاول اختيار المستخدم الحالي إن وُجد (نطابق fullName أو email)
      if (user?.email) {
        const me = data.find((e) => e.email === user.email);
        if (me) setSelectedEmployee(me.id);
      }
    } catch {
      // ignore
    }
  };

  const loadHistory = async () => {
    setLoadingHistory(true);
    try {
      const data = await hrApi.listAttendance({ employeeId: selectedEmployee });
      // آخر 20 سجل
      setHistory(data.slice(-20).reverse());
    } catch (e: unknown) {
      setMessage({ type: 'error', text: getErrorMessage(e, 'تعذّر تحميل السجل.') });
    } finally {
      setLoadingHistory(false);
    }
  };

  const handleAction = async (type: 1 | 2) => {
    if (!selectedEmployee) {
      setMessage({ type: 'error', text: 'يجب اختيار الموظف أولاً.' });
      return;
    }
    setActionLoading(type);
    setMessage(null);
    try {
      await hrApi.recordAttendance({ employeeId: selectedEmployee, type });
      setMessage({
        type: 'success',
        text: type === 1 ? 'تم تسجيل الحضور بنجاح.' : 'تم تسجيل الانصراف بنجاح.',
      });
      loadHistory();
    } catch (e: unknown) {
      setMessage({ type: 'error', text: getErrorMessage(e, 'فشل تسجيل الحركة.') });
    } finally {
      setActionLoading(null);
    }
  };

  return (
    <div>
      <PageHeader
        title="🕐 الحضور والانصراف"
        description="سجل حضور الموظفين — CheckIn / CheckOut"
      />

      {message && (
        <div
          className={`px-4 py-3 rounded-lg mb-4 text-sm ${
            message.type === 'success'
              ? 'bg-green-50 border border-green-200 text-green-700'
              : 'bg-red-50 border border-red-200 text-red-700'
          }`}
        >
          {message.text}
        </div>
      )}

      {/* Action Card */}
      <Card title="تسجيل حركة" description="اختر الموظف ثم اضغط CheckIn / CheckOut" className="mb-4">
        <div className="flex flex-wrap items-end gap-3">
          <div className="flex-1 min-w-[200px]">
            <Select
              label="الموظف"
              value={selectedEmployee}
              onChange={(e) => setSelectedEmployee(e.target.value)}
              options={employees.map((e) => ({ label: `${e.fullName} (${e.employeeNumber})`, value: e.id }))}
              placeholder="اختر الموظف"
            />
          </div>
          <Button
            variant="primary"
            onClick={() => handleAction(1)}
            loading={actionLoading === 1}
            disabled={!selectedEmployee}
            iconLeft={<LogIn className="h-4 w-4" />}
          >
            CheckIn
          </Button>
          <Button
            variant="danger"
            onClick={() => handleAction(2)}
            loading={actionLoading === 2}
            disabled={!selectedEmployee}
            iconLeft={<LogOutIcon className="h-4 w-4" />}
          >
            CheckOut
          </Button>
        </div>
      </Card>

      {/* History */}
      <Card title="السجل الأخير" description={`آخر 20 حركة للموظف المحدد`}>
        <Table
          columns={[
            {
              key: 'timestamp',
              header: 'الوقت',
              render: (r) => (
                <div>
                  <p className="text-sm text-gray-800">{formatDate(r.timestamp)}</p>
                  <p className="text-xs text-gray-500 font-mono">
                    {formatTime(r.timestamp)}
                  </p>
                </div>
              ),
            },
            {
              key: 'type',
              header: 'النوع',
              render: (r) => (
                <Badge variant={r.type === 1 ? 'success' : 'danger'}>
                  {r.type === 1 ? (
                    <>
                      <LogIn className="h-3 w-3 ml-1" /> {ATTENDANCE_TYPES[r.type]}
                    </>
                  ) : (
                    <>
                      <LogOutIcon className="h-3 w-3 ml-1" /> {ATTENDANCE_TYPES[r.type]}
                    </>
                  )}
                </Badge>
              ),
            },
            {
              key: 'notes',
              header: 'ملاحظات',
              render: (r) => r.notes || <span className="text-gray-400 text-xs">—</span>,
            },
            // v1.0.24: edit/delete actions for attendance records
            {
              key: 'actions',
              header: 'إجراءات',
              align: 'center',
              render: (r) => (
                <EntityActions
                  itemLabel={`حركة ${ATTENDANCE_TYPES[r.type] || r.type} (${formatDate(r.timestamp)})`}
                  hideEdit
                  onDelete={async () => {
                    try {
                      await hrApi.deleteAttendance(r.id);
                      await loadHistory();
                    } catch (e) {
                      setError(getErrorMessage(e, 'فشل حذف الحركة.'));
                    }
                  }}
                />
              ),
            },
          ]}
          data={history}
          loading={loadingHistory}
          rowKey={(r) => r.id}
          emptyMessage={
            !selectedEmployee ? 'اختر موظفاً لعرض السجل.' : 'لا توجد حركات مسجلة لهذا الموظف.'
          }
        />
      </Card>
    </div>
  );
}


