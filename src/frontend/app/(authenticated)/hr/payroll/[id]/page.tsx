'use client';

// صفحة تفاصيل دورة رواتب — header + جدول payslips + actions (Process/Post)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { ArrowRight, PlayCircle, CheckCircle2, FileText, RefreshCw, Receipt } from 'lucide-react';
import { Button, Table, Badge, Card, PageHeader } from '@/components/ui';
import {
  hrApi,
  PayrollRun,
  PayrollItem,
  PAYROLL_RUN_STATUSES,
  PAYROLL_RUN_STATUS_VARIANTS,
  PAYROLL_ITEM_STATUSES,
  getErrorMessage,
} from '@/lib/api';

interface PageProps {
  params: { id: string };
}

export default function PayrollRunDetailPage({ params }: PageProps) {
  const { id } = params;

  const [run, setRun] = useState<PayrollRun | null>(null);
  const [items, setItems] = useState<PayrollItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actionLoading, setActionLoading] = useState<string | null>(null);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const [runData, itemsData] = await Promise.all([
        hrApi.payroll.getPayrollRun(id),
        hrApi.payroll.getPayrollRunItems(id).catch(() => [] as PayrollItem[]),
      ]);
      setRun(runData);
      setItems(itemsData);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الدورة.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  const onProcess = async () => {
    if (!run) return;
    if (!confirm('معالجة الدورة: سيتم حساب payslips لكل الموظفين النشطين. متابعة؟')) return;
    setActionLoading('process');
    try {
      const updated = await hrApi.payroll.processPayrollRun(run.id);
      setRun(updated);
      // reload items to see the new payslips
      const its = await hrApi.payroll.getPayrollRunItems(run.id);
      setItems(its);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشلت المعالجة.'));
    } finally {
      setActionLoading(null);
    }
  };

  const onPost = async () => {
    if (!run) return;
    if (!confirm('ترحيل الدورة: سيتم إنشاء قيد محاسبي (Dr Salary / Cr Cash). لا يمكن التراجع! متابعة؟')) return;
    setActionLoading('post');
    try {
      const updated = await hrApi.payroll.postPayrollRun(run.id);
      setRun(updated);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل الترحيل.'));
    } finally {
      setActionLoading(null);
    }
  };

  const formatDate = (s?: string) => (s ? new Date(s).toLocaleDateString('en-GB') : '—');  // DEC-077: BUG-008 — use en-GB (dd/MM/yyyy)
  const formatMoney = (n: number) => n?.toLocaleString(undefined, { minimumFractionDigits: 2 }) || '0.00';

  if (loading) {
    return (
      <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
        <p className="mt-3 text-sm">جاري التحميل...</p>
      </div>
    );
  }

  if (!run) {
    return (
      <div>
        <PageHeader
          title="الدورة غير موجودة"
          breadcrumb={[
            { label: 'Payroll', href: '/hr/payroll' },
            { label: 'غير موجودة' },
          ]}
          actions={
            <Link href="/hr/payroll">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
                رجوع
              </Button>
            </Link>
          }
        />
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">
          {error || 'لم يتم العثور على الدورة.'}
        </div>
      </div>
    );
  }

  return (
    <div>
      <PageHeader
        title={`💰 دورة رواتب`}
        description={`الفترة: ${formatDate(run.periodStart)} → ${formatDate(run.periodEnd)}`}
        breadcrumb={[
          { label: 'Payroll', href: '/hr/payroll' },
          { label: formatDate(run.periodStart) },
        ]}
        actions={
          <Link href="/hr/payroll">
            <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>
              رجوع
            </Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">
          {error}
        </div>
      )}

      {/* Header card: الحالة + totals + actions */}
      <Card className="mb-6" accent="blue">
        <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
          <div>
            <p className="text-xs text-gray-500 mb-1">الحالة</p>
            <Badge variant={PAYROLL_RUN_STATUS_VARIANTS[run.status] || 'neutral'} size="md">
              {run.status === 2 && <PlayCircle className="h-3 w-3 ml-1" />}
              {run.status === 3 && <CheckCircle2 className="h-3 w-3 ml-1" />}
              {PAYROLL_RUN_STATUSES[run.status] || run.status}
            </Badge>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">عدد Payslips</p>
            <p className="font-bold text-gray-800">{run.itemsCount ?? items.length}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">إجمالي Gross</p>
            <p className="font-mono font-bold text-gray-800">{formatMoney(run.totalGross)}</p>
          </div>
          <div>
            <p className="text-xs text-gray-500 mb-1">إجمالي Net</p>
            <p className="font-mono font-bold text-green-700">{formatMoney(run.totalNet)}</p>
          </div>
        </div>

        {/* تاريخ المعالجة + الترحيل */}
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4 pt-4 border-t border-gray-100 text-xs text-gray-600">
          <div>
            <span className="text-gray-400">تاريخ الإنشاء: </span>
            <span className="font-mono">{formatDate(run.createdAt)}</span>
          </div>
          {run.processedAt && (
            <div>
              <span className="text-gray-400">تاريخ المعالجة: </span>
              <span className="font-mono">{formatDate(run.processedAt)}</span>
            </div>
          )}
          {run.postedAt && (
            <div>
              <span className="text-gray-400">تاريخ الترحيل: </span>
              <span className="font-mono">{formatDate(run.postedAt)}</span>
            </div>
          )}
        </div>

        {/* Actions */}
        <div className="flex items-center gap-2 mt-4 pt-4 border-t border-gray-100">
          <Button
            variant="outline"
            size="sm"
            onClick={load}
            disabled={loading}
            iconLeft={<RefreshCw className="h-4 w-4" />}
          >
            تحديث
          </Button>

          {/* Process: Draft → Processing */}
          {run.status === 1 && (
            <Button
              variant="primary"
              size="sm"
              onClick={onProcess}
              loading={actionLoading === 'process'}
              iconLeft={<PlayCircle className="h-4 w-4" />}
            >
              معالجة (Process)
            </Button>
          )}

          {/* Post: Processing → Posted */}
          {run.status === 2 && (
            <Button
              variant="primary"
              size="sm"
              onClick={onPost}
              loading={actionLoading === 'post'}
              iconLeft={<Receipt className="h-4 w-4" />}
            >
              ترحيل (Post → GL)
            </Button>
          )}

          {run.status === 3 && (
            <Badge variant="success" size="md">
              <CheckCircle2 className="h-3 w-3 ml-1" /> مُرحَّل — لا يمكن التعديل
            </Badge>
          )}

          {run.status === 4 && (
            <Badge variant="danger" size="md">
              ملغي
            </Badge>
          )}
        </div>

        {run.notes && (
          <div className="mt-3 text-xs text-gray-600 bg-gray-50 rounded px-3 py-2">
            <span className="font-semibold">ملاحظات: </span>
            {run.notes}
          </div>
        )}
      </Card>

      {/* جدول payslips */}
      <h2 className="text-lg font-bold text-gray-800 mb-3">قسائم الرواتب (Payslips)</h2>

      {items.length === 0 ? (
        <Card>
          <div className="text-center py-8 text-gray-500">
            <FileText className="h-10 w-10 mx-auto text-gray-300 mb-2" />
            <p className="text-sm">لا توجد payslips بعد.</p>
            {run.status === 1 && (
              <p className="text-xs text-gray-400 mt-1">
                اضغط <strong>معالجة (Process)</strong> لحساب payslips لكل الموظفين.
              </p>
            )}
            {run.status === 2 && items.length === 0 && (
              <p className="text-xs text-gray-400 mt-1">
                الدورة فارغة — لا يوجد موظفين نشطين في هذه الفترة.
              </p>
            )}
          </div>
        </Card>
      ) : (
        <Table
          columns={[
            {
              key: 'employee',
              header: 'الموظف',
              render: (i) => (
                <div>
                  <p className="font-semibold text-gray-800">{i.employeeName || i.employeeId}</p>
                  <p className="text-xs text-gray-500 font-mono mt-0.5">
                    {i.employeeNumber || i.employeeId.slice(0, 8)}
                  </p>
                </div>
              ),
            },
            {
              key: 'gross',
              header: 'Gross',
              align: 'end',
              render: (i) => <span className="font-mono text-sm">{formatMoney(i.grossSalary)}</span>,
            },
            {
              key: 'tax',
              header: 'ضريبة',
              align: 'end',
              render: (i) => (
                <span className="font-mono text-sm text-red-600">−{formatMoney(i.taxAmount)}</span>
              ),
            },
            {
              key: 'si',
              header: 'تأمينات',
              align: 'end',
              render: (i) => (
                <span className="font-mono text-sm text-red-600">
                  −{formatMoney(i.socialInsuranceEmployee)}
                </span>
              ),
            },
            {
              key: 'net',
              header: 'Net',
              align: 'end',
              render: (i) => (
                <span className="font-mono text-sm font-bold text-green-700">
                  {formatMoney(i.netSalary)}
                </span>
              ),
            },
            {
              key: 'days',
              header: 'أيام',
              align: 'center',
              render: (i) => <span className="text-sm text-gray-700">{i.paymentDays}</span>,
            },
            {
              key: 'status',
              header: 'الحالة',
              align: 'center',
              render: (i) => (
                <Badge variant={i.status === 3 ? 'success' : i.status === 2 ? 'info' : 'neutral'}>
                  {PAYROLL_ITEM_STATUSES[i.status] || i.status}
                </Badge>
              ),
            },
            {
              key: 'actions',
              header: '',
              align: 'center',
              render: (i) => (
                <Link href={`/hr/payroll/${run.id}/payslip/${i.employeeId}`}>
                  <Button variant="ghost" size="sm" iconLeft={<FileText className="h-4 w-4" />}>
                    عرض Payslip
                  </Button>
                </Link>
              ),
            },
          ]}
          data={items}
          loading={false}
          rowKey={(i) => i.id}
          emptyMessage="لا توجد payslips."
        />
      )}

      {/* Footer note */}
      {!loading && items.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">{items.length} قسيمة</p>
      )}
    </div>
  );
}