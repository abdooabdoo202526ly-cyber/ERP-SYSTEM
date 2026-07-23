'use client';

// صفحة تقرير أعمار الذمم المدينة (AR Aging) — جدول per-customer + 5 buckets

import { useEffect, useState } from 'react';
import { Clock, Users } from 'lucide-react';
import { Card, PageHeader, Input, Table, Badge } from '@/components/ui';
import { arApi, ArAgingReport, getErrorMessage } from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function AgingArPage() {
  const [asOfDate, setAsOfDate] = useState<string>(new Date().toISOString().slice(0, 10));
  const [report, setReport] = useState<ArAgingReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = async (date: string) => {
    setLoading(true);
    setError(null);
    try {
      const isoDate = new Date(date).toISOString();
      const r = await arApi.aging(isoDate);
      setReport(r);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل التقرير.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(asOfDate); }, []);

  const onDateChange = (newDate: string) => {
    setAsOfDate(newDate);
    load(newDate);
  };

  return (
    <div>
      <PageHeader
        title="⏳ أعمار الذمم المدينة (AR Aging)"
        description="تقرير أعمار ديون العملاء موزعة على 5 فئات زمنية"
        actions={
          <div className="flex items-center gap-2">
            <Clock className="h-4 w-4 text-gray-500" />
            <span className="text-sm text-gray-600">حتى تاريخ:</span>
            <Input
              type="date"
              value={asOfDate}
              onChange={(e) => onDateChange(e.target.value)}
              containerClassName="w-44"
            />
          </div>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      {report && (
        <Card className="mb-4">
          <div className="flex items-center gap-2 mb-3">
            <Users className="h-4 w-4 text-gray-500" />
            <h3 className="font-bold text-gray-800">الإجمالي الكلي</h3>
            <Badge variant="info">{report.rows.length} عميل</Badge>
            <span className="text-sm text-gray-500 mr-2">بتاريخ {formatDate(report.asOfDate)}</span>
          </div>
          <div className="grid grid-cols-2 md:grid-cols-6 gap-3 text-center">
            <div className="bg-green-50 rounded-lg p-3">
              <p className="text-xs text-gray-500">0-30 يوم</p>
              <p className="font-mono font-bold text-green-700">{formatNumber(report.grandTotal.bucket0To30)}</p>
            </div>
            <div className="bg-yellow-50 rounded-lg p-3">
              <p className="text-xs text-gray-500">31-60 يوم</p>
              <p className="font-mono font-bold text-yellow-700">{formatNumber(report.grandTotal.bucket31To60)}</p>
            </div>
            <div className="bg-orange-50 rounded-lg p-3">
              <p className="text-xs text-gray-500">61-90 يوم</p>
              <p className="font-mono font-bold text-orange-700">{formatNumber(report.grandTotal.bucket61To90)}</p>
            </div>
            <div className="bg-red-50 rounded-lg p-3">
              <p className="text-xs text-gray-500">91-120 يوم</p>
              <p className="font-mono font-bold text-red-700">{formatNumber(report.grandTotal.bucket91To120)}</p>
            </div>
            <div className="bg-red-100 rounded-lg p-3">
              <p className="text-xs text-gray-500">+120 يوم</p>
              <p className="font-mono font-bold text-red-800">{formatNumber(report.grandTotal.bucket120Plus)}</p>
            </div>
            <div className="bg-blue-50 rounded-lg p-3">
              <p className="text-xs text-gray-500">المجموع</p>
              <p className="font-mono font-bold text-blue-700">{formatNumber(report.grandTotal.total)}</p>
            </div>
          </div>
        </Card>
      )}

      <Table
        columns={[
          {
            key: 'customer',
            header: 'العميل',
            render: (r) => (
              <div>
                <p className="font-mono text-xs text-gray-500">{r.customerCode}</p>
                <p className="font-semibold text-gray-800">{r.customerName}</p>
              </div>
            ),
          },
          {
            key: 'b0_30',
            header: '0-30',
            align: 'end',
            render: (r) => <span className="font-mono text-green-700">{formatNumber(r.buckets.bucket0To30)}</span>,
          },
          {
            key: 'b31_60',
            header: '31-60',
            align: 'end',
            render: (r) => <span className="font-mono text-yellow-700">{formatNumber(r.buckets.bucket31To60)}</span>,
          },
          {
            key: 'b61_90',
            header: '61-90',
            align: 'end',
            render: (r) => <span className="font-mono text-orange-700">{formatNumber(r.buckets.bucket61To90)}</span>,
          },
          {
            key: 'b91_120',
            header: '91-120',
            align: 'end',
            render: (r) => <span className="font-mono text-red-700">{formatNumber(r.buckets.bucket91To120)}</span>,
          },
          {
            key: 'b120p',
            header: '+120',
            align: 'end',
            render: (r) => <span className="font-mono text-red-800 font-bold">{formatNumber(r.buckets.bucket120Plus)}</span>,
          },
          {
            key: 'total',
            header: 'المجموع',
            align: 'end',
            render: (r) => <span className="font-mono font-bold text-blue-700">{formatNumber(r.buckets.total)}</span>,
          },
        ]}
        data={report?.rows || []}
        loading={loading}
        rowKey={(r) => r.customerId}
        emptyMessage="لا توجد ذمم مدينة قائمة."
      />
    </div>
  );
}
