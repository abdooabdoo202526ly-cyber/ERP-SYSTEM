'use client';

// صفحة تفاصيل الموظف (Employee Detail) — كل بيانات الموظف + آخر 3 payslips +
//   آخر 3 إجازات + آخر 5 سجلات حضور + أزرار Edit / Deactivate.

import { useEffect, useState, useCallback } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import {
  ArrowRight,
  Edit3,
  PowerOff,
  Mail,
  Phone,
  Contact,
  CalendarDays,
  Briefcase,
  Building2,
  DollarSign,
  FileText,
  CalendarOff,
  Clock,
  TrendingUp,
  TrendingDown,
  LogIn,
  LogOut as LogOutIcon,
  RefreshCw,
} from 'lucide-react';
import { Button, Card, Badge, Table, PageHeader } from '@/components/ui';
import {
  hrApi,
  Employee,
  Department,
  LeaveRequest,
  AttendanceRecord,
  PayrollItem,
  LEAVE_TYPES,
  LEAVE_STATUSES,
  LEAVE_STATUS_VARIANTS,
  ATTENDANCE_TYPES,
  getErrorMessage,
} from '@/lib/api';
import { formatDate, formatDateTime, formatTime } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

const MAX_PAYSLIPS = 3;
const MAX_LEAVES = 3;
const MAX_ATTENDANCE = 5;

export default function EmployeeDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params.id;

  const [employee, setEmployee] = useState<Employee | null>(null);
  const [department, setDepartment] = useState<Department | null>(null);

  const [recentPayslips, setRecentPayslips] = useState<PayrollItem[]>([]);
  const [recentLeaves, setRecentLeaves] = useState<LeaveRequest[]>([]);
  const [recentAttendance, setRecentAttendance] = useState<AttendanceRecord[]>([]);

  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<'deactivate' | 'refresh' | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const emp = await hrApi.getEmployee(id);
      setEmployee(emp);

      // Department (link)
      let dept: Department | null = null;
      if (emp.departmentId) {
        try {
          const depts = await hrApi.listDepartments();
          dept = depts.find((d) => d.id === emp.departmentId) || null;
        } catch {
          // best-effort
        }
      }
      setDepartment(dept);

      // Recent leaves (filtered + last 3)
      try {
        const allLeaves = await hrApi.listLeaves();
        setRecentLeaves(
          allLeaves
            .filter((l) => l.employeeId === emp.id)
            .sort((a, b) => new Date(b.startDate).getTime() - new Date(a.startDate).getTime())
            .slice(0, MAX_LEAVES)
        );
      } catch {
        setRecentLeaves([]);
      }

      // Recent attendance (last 5)
      try {
        const att = await hrApi.listAttendance({ employeeId: emp.id });
        setRecentAttendance(
          att
            .sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime())
            .slice(0, MAX_ATTENDANCE)
        );
      } catch {
        setRecentAttendance([]);
      }

      // Recent payslips — أفضل طريقة: نمسك آخر 3 دورات ثم فلترة payslips الموظف.
      // لكن الـ BE لا يكشف endpoint "payslips by employee" خارج سياق الـ run.
      // لذلك نُحاول آخر 5 runs؛ إذا أيٌّ منها يحتوي payslip للموظف، نُجمّع.
      try {
        const runs = await hrApi.payroll.listPayrollRuns();
        const last5 = runs.slice(0, 5);
        const allItems: PayrollItem[] = [];
        for (const run of last5) {
          try {
            const items = await hrApi.payroll.getPayrollRunItems(run.id);
            for (const it of items) {
              if (it.employeeId === emp.id) {
                allItems.push({ ...it, payrollRunId: run.id });
              }
            }
          } catch {
            // run may not be processed yet — skip
          }
        }
        // ترتيب حسب فترة الدورة (الأحدث أولاً)
        const withRun = allItems.map((it) => {
          const run = last5.find((r) => r.id === it.payrollRunId);
          return { item: it, runStart: run ? new Date(run.periodStart).getTime() : 0 };
        });
        withRun.sort((a, b) => b.runStart - a.runStart);
        setRecentPayslips(withRun.slice(0, MAX_PAYSLIPS).map((x) => x.item));
      } catch {
        setRecentPayslips([]);
      }
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الموظف.'));
    } finally {
      setLoading(false);
    }
  }, [id]);

  useEffect(() => {
    load();
  }, [load]);

  const onDeactivate = async () => {
    if (!employee) return;
    const label = employee.isActive ? 'إلغاء التفعيل' : 'إعادة التفعيل';
    if (!confirm(`${label} الموظف "${employee.fullName}"؟`)) return;
    setActionLoading('deactivate');
    try {
      // الباك اند يدعم DELETE (soft-deactivate). لإعادة التفعيل، نُرجع isActive=true
      // عبر update endpoint. لكن في هذه الـ phase: فقط deactivate مدعوم على مستوى الـ
      // route DELETE — لإعادة التفعيل نحتاج PUT كامل. نبقيها deactivate فقط هنا.
      if (employee.isActive) {
        await hrApi.deactivateEmployee(employee.id);
      } else {
        // re-activate عبر updateEmployee
        await hrApi.updateEmployee(employee.id, {
          fullName: employee.fullName,
          email: employee.email,
          phone: employee.phone,
          nationalId: employee.nationalId,
          departmentId: employee.departmentId,
          jobTitle: employee.jobTitle,
          hireDate: employee.hireDate,
          terminationDate: employee.terminationDate,
          baseSalary: employee.baseSalary,
          isActive: true,
        });
      }
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, `فشل ${label}.`));
    } finally {
      setActionLoading(null);
    }
  };

  if (loading) {
    return (
      <div>
        <PageHeader title="👤 الموظف" description="جاري التحميل..." />
        <Card>
          <div className="text-center py-12 text-gray-500">
            <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
            <p className="mt-3 text-sm">جاري التحميل...</p>
          </div>
        </Card>
      </div>
    );
  }

  if (!employee) {
    return (
      <div>
        <PageHeader
          title="الموظف غير موجود"
          breadcrumb={[
            { label: 'الرئيسية', href: '/dashboard' },
            { label: 'الموظفين', href: '/hr/employees' },
            { label: 'غير موجود' },
          ]}
          actions={
            <Link href="/hr/employees">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
          {error || 'لم يتم العثور على الموظف.'}
        </div>
      </div>
    );
  }

  // اسم عربي/إنجليزي — حالياً الـ schema يحمل fullName واحد. نُظهره مرّتين (نفسه)
  // لتغطية "Arabic + English" في الـ header مع مَركَز placeholder للـ English.
  const fullNameAr = employee.fullName;
  const fullNameEn = employee.fullName;

  return (
    <div>
      <PageHeader
        title={`👤 ${employee.fullName}`}
        description={`${employee.jobTitle || 'بدون مسمى'} • ${employee.employeeNumber}`}
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الموظفين', href: '/hr/employees' },
          { label: employee.fullName },
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
            <Link href={`/hr/employees/${employee.id}/edit`}>
              <Button variant="primary" size="sm" iconLeft={<Edit3 className="h-4 w-4" />}>
                تعديل
              </Button>
            </Link>
            <Button
              variant={employee.isActive ? 'danger' : 'secondary'}
              size="sm"
              onClick={onDeactivate}
              loading={actionLoading === 'deactivate'}
              iconLeft={<PowerOff className="h-4 w-4" />}
            >
              {employee.isActive ? 'إلغاء التفعيل' : 'إعادة التفعيل'}
            </Button>
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {/* Header card: الاسم + الحالة + أزرار */}
      <Card className="mb-6" accent="blue">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div className="md:col-span-2">
            <p className="text-xs text-gray-500 mb-1">الاسم (عربي)</p>
            <p className="text-lg font-bold text-gray-800">{fullNameAr || '—'}</p>
            <p className="text-xs text-gray-500 mt-1 mb-0.5">الاسم (English)</p>
            <p className="text-sm text-gray-700 font-mono" dir="ltr">{fullNameEn || '—'}</p>
            <p className="text-xs text-gray-500 font-mono mt-1">Code: {employee.employeeNumber}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">الحالة</p>
            {employee.isActive ? (
              <Badge variant="success" size="md">نشط</Badge>
            ) : (
              <Badge variant="neutral" size="md">غير نشط</Badge>
            )}
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">الراتب الأساسي</p>
            <p className="font-mono font-bold text-gray-800 text-lg">
              {formatNumber(employee.baseSalary)} LYD
            </p>
          </div>
        </div>
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mb-6">
        {/* Contact & Identity */}
        <Card title="التواصل والهوية" accent="none">
          <div className="space-y-3 text-sm">
            <div className="flex items-start gap-2">
              <Mail className="h-4 w-4 text-gray-400 mt-0.5" />
              <div className="min-w-0 flex-1">
                <p className="text-xs text-gray-500">البريد الإلكتروني</p>
                {employee.email ? (
                  <a
                    href={`mailto:${employee.email}`}
                    className="text-blue-600 hover:underline break-all"
                  >
                    {employee.email}
                  </a>
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <Phone className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">الهاتف</p>
                {employee.phone ? (
                  <span className="font-mono" dir="ltr">{employee.phone}</span>
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <Contact className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">الرقم الوطني</p>
                {employee.nationalId ? (
                  <span className="font-mono">{employee.nationalId}</span>
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </div>
            </div>
          </div>
        </Card>

        {/* Work */}
        <Card title="البيانات الوظيفية" accent="green">
          <div className="space-y-3 text-sm">
            <div className="flex items-start gap-2">
              <Briefcase className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">المسمى الوظيفي</p>
                <p className="font-semibold text-gray-800">{employee.jobTitle || '—'}</p>
              </div>
            </div>
            <div className="flex items-start gap-2">
              <Building2 className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">القسم</p>
                {employee.departmentId ? (
                  <Link
                    href={`/hr/departments/${employee.departmentId}`}
                    className="text-blue-600 hover:underline"
                  >
                    {department?.name || employee.departmentId.slice(0, 8)}
                  </Link>
                ) : (
                  <span className="text-gray-400">—</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <CalendarDays className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">تاريخ التعيين</p>
                <p className="font-semibold text-gray-800">{formatDate(employee.hireDate)}</p>
              </div>
            </div>
            <div className="flex items-start gap-2">
              <CalendarOff className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">تاريخ نهاية الخدمة</p>
                {employee.terminationDate ? (
                  <p className="font-semibold text-gray-800">{formatDate(employee.terminationDate)}</p>
                ) : (
                  <span className="text-gray-400">— لا يوجد —</span>
                )}
              </div>
            </div>
          </div>
        </Card>

        {/* Salary structure & Manager */}
        <Card title="الرواتب والمدراء" accent="purple">
          <div className="space-y-3 text-sm">
            <div className="flex items-start gap-2">
              <DollarSign className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">هيكل الراتب (Salary Structure)</p>
                {/*
                  الباك اند لا يربط الموظف بهيكل راتب بعدُ — يُعرض placeholder.
                  عند إضافة salaryStructureId لاحقاً، يصبح رابط إلى /hr/salary-structures/[id].
                */}
                <span className="text-gray-400">— غير مُعيَّن —</span>
              </div>
            </div>
            <div className="flex items-start gap-2">
              <Briefcase className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">المدير المباشر</p>
                {/* الـ BE لا يحمل managerId للموظف — نُظهر مدير القسم كقيمة بديلة */}
                {department?.managerId ? (
                  <span className="font-mono text-xs">
                    {department.managerId.slice(0, 8)}...
                    <span className="text-gray-400 text-[10px] block">مدير القسم</span>
                  </span>
                ) : (
                  <span className="text-gray-400">— غير مُعيَّن —</span>
                )}
              </div>
            </div>
            <div className="flex items-start gap-2">
              <FileText className="h-4 w-4 text-gray-400 mt-0.5" />
              <div>
                <p className="text-xs text-gray-500">رقم الموظف</p>
                <p className="font-mono text-sm">{employee.employeeNumber}</p>
              </div>
            </div>
            <div className="text-[10px] text-gray-400 pt-2 border-t border-gray-100">
              أُنشئ: {formatDate(employee.createdAt)}
            </div>
          </div>
        </Card>
      </div>

      {/* Recent payslips (last 3) */}
      <Card
        className="mb-6"
        title={
          <span className="flex items-center gap-2">
            <TrendingUp className="h-4 w-4 text-green-600" /> آخر 3 قسائم راتب
          </span>
        }
        accent="green"
        actions={
          <Link href="/hr/payroll">
            <Button variant="ghost" size="sm">عرض كل الـ Payroll</Button>
          </Link>
        }
      >
        {recentPayslips.length === 0 ? (
          <p className="text-sm text-gray-500 text-center py-4">
            لا توجد قسائم رواتب مسجلة بعد.
          </p>
        ) : (
          <Table
            columns={[
              {
                key: 'run',
                header: 'الدورة',
                render: (p) => (
                  <Link
                    href={`/hr/payroll/${p.payrollRunId}/payslip/${p.employeeId}`}
                    className="text-blue-600 hover:underline font-mono text-xs"
                  >
                    {p.payrollRunId.slice(0, 8)}...
                  </Link>
                ),
              },
              {
                key: 'gross',
                header: 'Gross',
                align: 'end',
                render: (p) => <span className="font-mono text-sm">{formatNumber(p.grossSalary)}</span>,
              },
              {
                key: 'tax',
                header: 'ضريبة',
                align: 'end',
                render: (p) => (
                  <span className="font-mono text-sm text-red-600">−{formatNumber(p.taxAmount)}</span>
                ),
              },
              {
                key: 'si',
                header: 'تأمينات',
                align: 'end',
                render: (p) => (
                  <span className="font-mono text-sm text-red-600">
                    −{formatNumber(p.socialInsuranceEmployee)}
                  </span>
                ),
              },
              {
                key: 'net',
                header: 'Net',
                align: 'end',
                render: (p) => (
                  <span className="font-mono text-sm font-bold text-green-700">
                    {formatNumber(p.netSalary)}
                  </span>
                ),
              },
              {
                key: 'view',
                header: '',
                align: 'center',
                render: (p) => (
                  <Link href={`/hr/payroll/${p.payrollRunId}/payslip/${p.employeeId}`}>
                    <Button variant="ghost" size="sm" iconLeft={<FileText className="h-4 w-4" />}>
                      عرض
                    </Button>
                  </Link>
                ),
              },
            ]}
            data={recentPayslips}
            rowKey={(p) => `${p.payrollRunId}-${p.employeeId}`}
            emptyMessage="لا توجد قسائم بعد."
          />
        )}
      </Card>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-4">
        {/* Recent leaves (last 3) */}
        <Card
          title={
            <span className="flex items-center gap-2">
              <CalendarOff className="h-4 w-4 text-orange-600" /> آخر 3 إجازات
            </span>
          }
          accent="yellow"
        >
          {recentLeaves.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">لا توجد طلبات إجازات.</p>
          ) : (
            <Table
              columns={[
                {
                  key: 'type',
                  header: 'النوع',
                  render: (l) => (
                    <Badge variant="info">{LEAVE_TYPES[l.leaveType] || l.leaveType}</Badge>
                  ),
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
                  key: 'status',
                  header: 'الحالة',
                  render: (l) => (
                    <Badge variant={LEAVE_STATUS_VARIANTS[l.status] || 'neutral'}>
                      {LEAVE_STATUSES[l.status] || l.status}
                    </Badge>
                  ),
                },
                {
                  key: 'view',
                  header: '',
                  align: 'center',
                  render: (l) => (
                    <Link href={`/hr/leaves/${l.id}`}>
                      <Button variant="ghost" size="sm">عرض</Button>
                    </Link>
                  ),
                },
              ]}
              data={recentLeaves}
              rowKey={(l) => l.id}
              emptyMessage="لا توجد إجازات."
            />
          )}
        </Card>

        {/* Recent attendance (last 5) */}
        <Card
          title={
            <span className="flex items-center gap-2">
              <Clock className="h-4 w-4 text-blue-600" /> آخر 5 سجلات حضور
            </span>
          }
          accent="blue"
          actions={
            <Link href="/hr/attendance">
              <Button variant="ghost" size="sm">صفحة الحضور</Button>
            </Link>
          }
        >
          {recentAttendance.length === 0 ? (
            <p className="text-sm text-gray-500 text-center py-4">لا توجد سجلات حضور.</p>
          ) : (
            <Table
              columns={[
                {
                  key: 'time',
                  header: 'الوقت',
                  render: (r) => (
                    <div>
                      <p className="text-sm text-gray-800">{formatDate(r.timestamp)}</p>
                      <p className="text-xs text-gray-500 font-mono">{formatTime(r.timestamp)}</p>
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
                  key: 'view',
                  header: '',
                  align: 'center',
                  render: (r) => (
                    <Link href={`/hr/attendance/${r.id}`}>
                      <Button variant="ghost" size="sm">عرض</Button>
                    </Link>
                  ),
                },
              ]}
              data={recentAttendance}
              rowKey={(r) => r.id}
              emptyMessage="لا توجد حركات."
            />
          )}
        </Card>
      </div>

      {/* EOS shortcut */}
      <div className="mt-6">
        <Card className="bg-blue-50/40 border-blue-100">
          <div className="flex items-center justify-between">
            <div>
              <p className="font-bold text-gray-800 flex items-center gap-2">
                <TrendingDown className="h-4 w-4 text-blue-600" /> مستحقات نهاية الخدمة (EOS)
              </p>
              <p className="text-xs text-gray-500 mt-1">
                حساب EOS للموظف على أساس الراتب الأساسي وسنوات الخدمة.
              </p>
            </div>
            <Button
              variant="outline"
              size="sm"
              onClick={async () => {
                try {
                  const eos = await hrApi.payroll.getEos(employee.id);
                  alert(
                    `EOS للموظف ${eos.employeeName || employee.fullName}:\n` +
                      `سنوات الخدمة: ${eos.yearsOfService}\n` +
                      `الراتب: ${formatNumber(eos.monthlySalary)} LYD\n` +
                      `المبلغ: ${formatNumber(eos.eosAmount)} LYD`
                  );
                } catch (e: unknown) {
                  alert(getErrorMessage(e, 'فشل حساب EOS.'));
                }
              }}
            >
              حساب EOS
            </Button>
          </div>
        </Card>
      </div>
    </div>
  );
}
