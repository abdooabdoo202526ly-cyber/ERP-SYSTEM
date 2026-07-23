'use client';

// قائمة مراكز التكلفة (Cost Centers)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Pencil } from 'lucide-react';
import { Card, Badge, PageHeader, Button, EntityActions } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage } from '@/lib/api';
import { api } from '@/lib/api';

interface CostCenter {
  id: string;
  tenantId: string;
  companyId: string;
  code: string;
  name: string;
  description?: string;
  type: number; // 1=Production, 2=Service, 3=Administrative, 4=Sales
  parentId?: string;
  isActive: boolean;
  createdAt: string;
}

const CC_TYPES: Record<number, string> = {
  1: 'إنتاج',
  2: 'خدمات',
  3: 'إداري',
  4: 'مبيعات',
};

export default function CostCentersPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<CostCenter[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const res = await fetch('/api/cost-centers', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="🏢 مراكز التكلفة"
        description="إدارة مراكز التكلفة في النظام"
        actions={
          <Link href="/finance/cost-centers/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              مركز جديد
            </Button>
          </Link>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">
          {error}
        </div>
      )}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          لا توجد مراكز تكلفة في هذا الـ tenant.
        </div>
      ) : (
        <div className="space-y-3">
          {items.map((cc) => (
            <Card key={cc.id} accent="blue">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-xs text-gray-500 font-mono">{cc.code}</p>
                  <h3 className="font-bold text-gray-800 mt-1 text-lg">{cc.name}</h3>
                  {cc.description && <p className="text-sm text-gray-500 mt-1">{cc.description}</p>}
                </div>
                <div className="flex items-center gap-1">
                  <EntityActions
                    editHref={`/finance/cost-centers/${cc.id}/edit`}
                    itemLabel={`${cc.code} — ${cc.name}`}
                    onDelete={async () => {
                      try {
                        await api.delete(`/api/cost-centers/${cc.id}`);
                        await load();
                      } catch (e) {
                        setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
                      }
                    }}
                  />
                  <Badge variant={cc.isActive ? 'success' : 'neutral'}>
                    {CC_TYPES[cc.type] || `Type ${cc.type}`}
                  </Badge>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
