'use client';

// تفاصيل إشعار (Notification Detail) + Mark as read

import { useEffect, useState } from 'react';
import { useParams } from 'next/navigation';
import Link from 'next/link';
import { ArrowRight, Check } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';

interface Notification {
  id: string;
  type: string;
  title: string;
  message: string;
  referenceType?: string;
  referenceId?: string;
  isRead: boolean;
  createdAt: string;
  readAt?: string;
}

const TYPE_BADGE: Record<string, { label: string; variant: 'info' | 'warning' | 'success' | 'danger' }> = {
  LowStock: { label: 'مخزون منخفض', variant: 'warning' },
  JournalPosted: { label: 'قيد مُرحَّل', variant: 'success' },
  HighVariance: { label: 'انحراف عالي', variant: 'danger' },
  Payroll: { label: 'رواتب', variant: 'info' },
  System: { label: 'نظام', variant: 'info' },
};

export default function NotificationDetailPage() {
  const params = useParams<{ id: string }>();
  useAuth();
  const [item, setItem] = useState<Notification | null>(null);
  const [loading, setLoading] = useState(true);
  const [actionRunning, setActionRunning] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const load = async () => {
    try {
      const res = await fetch('/api/inventory/notifications', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const list = await res.json();
      const items = Array.isArray(list) ? list : (list.items || []);
      const found = items.find((x: Notification) => x.id === params.id);
      if (!found) throw new Error('الإشعار غير موجود');
      setItem(found);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => { load(); /* eslint-disable-next-line */ }, [params.id]);

  const handleMarkRead = async () => {
    if (!item) return;
    setActionRunning(true);
    try {
      const res = await fetch(`/api/inventory/notifications/${item.id}/mark-read`, { method: 'POST' });
      if (!res.ok && res.status !== 204) throw new Error('فشل التعليم كمقروء');
      await load();
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التعليم.'));
    } finally {
      setActionRunning(false);
    }
  };

  if (loading) return <div><PageHeader title="إشعار" /><Card><div className="text-center py-12 text-gray-500">جاري التحميل...</div></Card></div>;
  if (!item) return <div><PageHeader title="إشعار" /><Card><div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg text-sm">{error || 'غير موجود'}</div><div className="mt-4"><Link href="/admin/notifications"><Button variant="ghost">رجوع</Button></Link></div></Card></div>;

  const t = TYPE_BADGE[item.type] || { label: item.type, variant: 'info' as const };

  return (
    <div>
      <PageHeader
        title="🔔 إشعار"
        breadcrumb={[
          { label: 'الرئيسية', href: '/dashboard' },
          { label: 'الإشعارات', href: '/admin/notifications' },
          { label: item.title.substring(0, 30) },
        ]}
        actions={
          <div className="flex items-center gap-2">
            {!item.isRead && (
              <Button variant="primary" onClick={handleMarkRead} loading={actionRunning} iconLeft={<Check className="h-4 w-4" />}>
                تعليم كمقروء
              </Button>
            )}
            <Link href="/admin/notifications">
              <Button variant="ghost" iconLeft={<ArrowRight className="h-4 w-4" />}>رجوع</Button>
            </Link>
          </div>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>}

      <Card className="max-w-3xl">
        <div className="grid grid-cols-2 gap-4 text-sm">
          <div>
            <p className="text-gray-500">النوع</p>
            <Badge variant={t.variant}>{t.label}</Badge>
          </div>
          <div>
            <p className="text-gray-500">الحالة</p>
            <Badge variant={item.isRead ? 'neutral' : 'warning'}>{item.isRead ? 'مقروء' : 'غير مقروء'}</Badge>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">العنوان</p>
            <p className="font-bold text-lg">{item.title}</p>
          </div>
          <div className="col-span-2">
            <p className="text-gray-500">الرسالة</p>
            <p className="text-gray-800">{item.message}</p>
          </div>
          {item.referenceType && (
            <>
              <div>
                <p className="text-gray-500">نوع المرجع</p>
                <Badge variant="info">{item.referenceType}</Badge>
              </div>
              <div>
                <p className="text-gray-500">معرّف المرجع</p>
                <p className="font-mono text-xs">{item.referenceId}</p>
              </div>
            </>
          )}
          <div>
            <p className="text-gray-500">تاريخ الإنشاء</p>
            <p className="font-mono">{new Date(item.createdAt).toLocaleString('en-GB')}</p>
          </div>
          {item.readAt && (
            <div>
              <p className="text-gray-500">تاريخ القراءة</p>
              <p className="font-mono">{new Date(item.readAt).toLocaleString('en-GB')}</p>
            </div>
          )}
        </div>
      </Card>
    </div>
  );
}
