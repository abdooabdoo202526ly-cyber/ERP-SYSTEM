'use client';

// قائمة الإشعارات (Notifications) — مع mark-read inline

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Check, Eye, Bell } from 'lucide-react';
import { Card, Badge, PageHeader, Button } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { notificationsApi, Notification, getErrorMessage } from '@/lib/api';

const TYPE_BADGE: Record<string, { label: string; variant: 'info' | 'warning' | 'success' | 'danger' }> = {
  LowStock: { label: 'مخزون منخفض', variant: 'warning' },
  JournalPosted: { label: 'قيد مُرحَّل', variant: 'success' },
  HighVariance: { label: 'انحراف عالي', variant: 'danger' },
  Payroll: { label: 'رواتب', variant: 'info' },
  System: { label: 'نظام', variant: 'info' },
};

export default function NotificationsPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<Notification[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [busy, setBusy] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading, unreadOnly]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await notificationsApi.list(unreadOnly);
      const list = Array.isArray(data) ? data : (data as unknown as { items?: Notification[] }).items || [];
      setItems(list);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  const onMarkRead = async (id: string) => {
    setBusy(id);
    try {
      await notificationsApi.markRead(id);
      // Optimistic local update
      setItems((prev) => prev.map((n) => (n.id === id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)));
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل تحديد كمقروء'));
    } finally {
      setBusy(null);
    }
  };

  const toggleUnread = () => setUnreadOnly(!unreadOnly);

  const unread = items.filter((n) => !n.isRead).length;
  const read = items.length - unread;

  return (
    <div>
      <PageHeader
        title="🔔 الإشعارات"
        description="إشعارات النظام للمستخدم"
        actions={
          <Button
            variant={unreadOnly ? 'primary' : 'ghost'}
            size="sm"
            onClick={toggleUnread}
            iconLeft={<Bell className="h-4 w-4" />}
          >
            {unreadOnly ? 'عرض الكل' : 'غير المقروءة فقط'}
          </Button>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>}

      {!loading && items.length > 0 && (
        <div className="grid grid-cols-2 gap-4 mb-4">
          <Card accent="red">
            <p className="text-gray-500 text-sm">غير مقروءة</p>
            <p className="font-bold text-3xl">{unread}</p>
          </Card>
          <Card accent="green">
            <p className="text-gray-500 text-sm">مقروءة</p>
            <p className="font-bold text-3xl">{read}</p>
          </Card>
        </div>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          {unreadOnly ? 'لا توجد إشعارات غير مقروءة.' : 'لا توجد إشعارات.'}
        </div>
      ) : (
        <div className="space-y-2">
          {items.map((n) => {
            const t = TYPE_BADGE[n.type] || { label: n.type, variant: 'info' as const };
            return (
              <Card key={n.id} accent={n.isRead ? 'gray' : 'yellow'}>
                <div className="flex items-start justify-between gap-3">
                  <div className="flex-1">
                    <div className="flex items-center gap-2 flex-wrap">
                      {!n.isRead && <span className="inline-block h-2 w-2 bg-red-500 rounded-full animate-pulse" />}
                      <h3 className={`font-bold ${n.isRead ? 'text-gray-600' : 'text-gray-900'}`}>{n.title}</h3>
                      <Badge variant={t.variant}>{t.label}</Badge>
                    </div>
                    <p className={`mt-1 text-sm ${n.isRead ? 'text-gray-500' : 'text-gray-700'}`}>{n.message}</p>
                    <p className="mt-2 text-xs text-gray-400">
                      📅 {new Date(n.createdAt).toLocaleString('en-GB')}
                      {n.referenceType && <> • {n.referenceType}</>}
                    </p>
                  </div>
                  <div className="flex items-center gap-1">
                    {!n.isRead && (
                      <Button
                        variant="ghost"
                        size="sm"
                        onClick={() => onMarkRead(n.id)}
                        loading={busy === n.id}
                        iconLeft={<Check className="h-3.5 w-3.5 text-green-600" />}
                        title="تحديد كمقروء"
                      >
                        <span className="text-green-700 text-xs">مقروء</span>
                      </Button>
                    )}
                    <Link href={`/admin/notifications/${n.id}`}>
                      <Button variant="ghost" size="sm" iconLeft={<Eye className="h-3.5 w-3.5" />}>
                        عرض
                      </Button>
                    </Link>
                  </div>
                </div>
              </Card>
            );
          })}
        </div>
      )}
    </div>
  );
}
