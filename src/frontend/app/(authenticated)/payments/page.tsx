'use client';

// صفحة قائمة المدفوعات (Payments) — AP (دفع مورّدين) + AR (مدفوعات/استردادات عملاء)
// تستهلك GET /api/payments مع فلاتر اختيارية: partyType / status

import { useEffect, useState, useMemo } from 'react';
import Link from 'next/link';
import { Plus, CreditCard, CheckCircle2, Search } from 'lucide-react';
import { Button, Input, Select, Table, Badge, PageHeader, Card } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import {
  paymentsApi,
  Payment,
  PAYMENT_STATUSES,
  PAYMENT_STATUS_VARIANTS,
  PAYMENT_METHODS,
  PAYMENT_PARTY_TYPES,
  getErrorMessage,
} from '@/lib/api';
import { formatDate } from '@/lib/utils';
import { formatNumber } from '@/lib/format';

export default function PaymentsPage() {
  const { loading: authLoading } = useAuth();
  const [payments, setPayments] = useState<Payment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [filterPartyType, setFilterPartyType] = useState<string>('');
  const [filterStatus, setFilterStatus] = useState<string>('');
  const [search, setSearch] = useState('');

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const params: { partyType?: string; status?: number } = {};
      if (filterPartyType) params.partyType = filterPartyType;
      if (filterStatus) params.status = Number(filterStatus);
      const data = await paymentsApi.list(params);
      setPayments(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل المدفوعات.'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (authLoading) return;
    load();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [authLoading, filterPartyType, filterStatus]);

  const postPayment = async (id: string, paymentNumber: string) => {
    if (!confirm(`سيتم ترحيل الدفعة ${paymentNumber} وإنشاء قيد محاسبي (Dr 2210 / Cr 1210 للمورّدين). هل أنت متأكد؟`)) return;
    try {
      await paymentsApi.post(id);
      await load();
    } catch (e: unknown) {
      alert(getErrorMessage(e, 'فشل ترحيل الدفعة.'));
    }
  };

  const filtered = useMemo(() => {
    if (!search) return payments;
    const q = search.toLowerCase();
    return payments.filter(
      (p) =>
        p.paymentNumber.toLowerCase().includes(q) ||
        p.partyType.toLowerCase().includes(q) ||
        p.paymentMethod.toLowerCase().includes(q)
    );
  }, [payments, search]);

  const partyTypeOptions = useMemo(
    () => [
      { value: '', label: 'كل الأطراف' },
      ...Object.entries(PAYMENT_PARTY_TYPES).map(([k, v]) => ({ value: k, label: v })),
    ],
    []
  );

  const statusOptions = useMemo(
    () => [
      { value: '', label: 'كل الحالات' },
      ...Object.entries(PAYMENT_STATUSES).map(([k, v]) => ({ value: k, label: v })),
    ],
    []
  );

  // إحصائيات سريعة
  const stats = useMemo(() => {
    const draft = payments.filter((p) => p.status === 1).length;
    const posted = payments.filter((p) => p.status === 2);
    const totalPosted = posted.reduce((s, p) => s + p.amount, 0);
    const totalAllocated = posted.reduce((s, p) => s + p.allocatedAmount, 0);
    const totalOnAccount = posted.reduce((s, p) => s + p.onAccountAmount, 0);
    return { draft, postedCount: posted.length, totalPosted, totalAllocated, totalOnAccount };
  }, [payments]);

  return (
    <div>
      <PageHeader
        title="💳 المدفوعات"
        description="سندات الدفع (AP) وسندات القبض/الاسترداد (AR) — مدعومة عبر /api/payments"
        actions={
          <Link href="/payments/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>دفعة جديدة</Button>
          </Link>
        }
      />

      {/* ملخص سريع */}
      {!loading && payments.length > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3 mb-4">
          <Card className="bg-yellow-50 border-yellow-200">
            <p className="text-xs text-yellow-700">مسودات</p>
            <p className="text-2xl font-bold text-yellow-900 mt-1">{stats.draft}</p>
          </Card>
          <Card className="bg-green-50 border-green-200">
            <p className="text-xs text-green-700">مُرحَّلة</p>
            <p className="text-2xl font-bold text-green-900 mt-1">{stats.postedCount}</p>
          </Card>
          <Card className="bg-blue-50 border-blue-200">
            <p className="text-xs text-blue-700">إجمالي المُرحَّل</p>
            <p className="text-2xl font-bold text-blue-900 mt-1">{formatNumber(stats.totalPosted)}</p>
          </Card>
          <Card className="bg-purple-50 border-purple-200">
            <p className="text-xs text-purple-700">دفعات مقدمة (On Account)</p>
            <p className="text-2xl font-bold text-purple-900 mt-1">{formatNumber(stats.totalOnAccount)}</p>
          </Card>
        </div>
      )}

      <Card className="mb-4">
        <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
          <div className="relative">
            <Search className="h-4 w-4 absolute right-3 top-1/2 -translate-y-1/2 text-gray-400" />
            <Input
              placeholder="🔍 بحث (رقم/طريقة)..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              className="pr-9"
            />
          </div>
          <Select
            value={filterPartyType}
            onChange={(e) => setFilterPartyType(e.target.value)}
            options={partyTypeOptions}
            placeholder="نوع الطرف"
          />
          <Select
            value={filterStatus}
            onChange={(e) => setFilterStatus(e.target.value)}
            options={statusOptions}
            placeholder="الحالة"
          />
        </div>
      </Card>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      <Table
        columns={[
          {
            key: 'paymentNumber',
            header: 'رقم الدفعة',
            render: (p) => (
              <Link href={`/payments/${p.id}`} className="font-mono font-semibold text-blue-600 hover:underline">
                {p.paymentNumber}
              </Link>
            ),
          },
          {
            key: 'partyType',
            header: 'نوع الطرف',
            align: 'center',
            render: (p) => (
              <Badge variant={p.partyType === 'Vendor' ? 'warning' : 'info'}>
                {PAYMENT_PARTY_TYPES[p.partyType] || p.partyType}
              </Badge>
            ),
          },
          {
            key: 'paymentDate',
            header: 'التاريخ',
            render: (p) => <span className="text-sm text-gray-600">{formatDate(p.paymentDate)}</span>,
          },
          {
            key: 'amount',
            header: 'المبلغ',
            align: 'end',
            render: (p) => <span className="font-mono font-bold">{formatNumber(p.amount)} {p.currencyCode}</span>,
          },
          {
            key: 'paymentMethod',
            header: 'الطريقة',
            render: (p) => p.paymentMethod ? <Badge variant="neutral">{PAYMENT_METHODS[p.paymentMethod] || p.paymentMethod}</Badge> : <span className="text-xs text-gray-400">—</span>,
          },
          {
            key: 'allocations',
            header: 'التخصيصات',
            align: 'end',
            render: (p) => (
              <div className="text-sm">
                <div className="font-mono text-gray-700">{formatNumber(p.allocatedAmount)}</div>
                {p.onAccountAmount > 0 && (
                  <div className="text-xs text-purple-600">+{formatNumber(p.onAccountAmount)} مقدمة</div>
                )}
              </div>
            ),
          },
          {
            key: 'status',
            header: 'الحالة',
            align: 'center',
            render: (p) => {
              const variant = PAYMENT_STATUS_VARIANTS[p.status] || 'neutral';
              return <Badge variant={variant}>{PAYMENT_STATUSES[p.status] || '—'}</Badge>;
            },
          },
          {
            key: 'actions',
            header: 'إجراءات',
            align: 'center',
            render: (p) => (
              <div className="flex items-center gap-1 justify-center">
                {p.status === 1 && (
                  <button
                    onClick={() => postPayment(p.id, p.paymentNumber)}
                    className="text-green-600 hover:text-green-800 p-1"
                    title="ترحيل"
                  >
                    <CheckCircle2 className="h-4 w-4" />
                  </button>
                )}
                {p.status === 2 && p.onAccountAmount > 0 && (
                  <Link
                    href={`/payments/${p.id}`}
                    className="text-blue-600 hover:text-blue-800 p-1"
                    title="تخصيص"
                  >
                    <CreditCard className="h-4 w-4" />
                  </Link>
                )}
              </div>
            ),
          },
        ]}
        data={filtered}
        loading={loading}
        rowKey={(p) => p.id}
        emptyMessage="لا توجد مدفوعات. أنشئ دفعة جديدة من الزر بالأعلى."
      />

      {!loading && filtered.length > 0 && (
        <p className="mt-3 text-xs text-gray-500 text-start">
          {filtered.length} دفعة • إجمالي: <span className="font-mono font-semibold">{formatNumber(filtered.reduce((s, p) => s + p.amount, 0))}</span>
        </p>
      )}
    </div>
  );
}
