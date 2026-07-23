'use client';

// قائمة فئات الأصناف (Item Categories)

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Pencil } from 'lucide-react';
import { Card, Badge, PageHeader, Button, EntityActions } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { getErrorMessage, inventoryApi, api } from '@/lib/api';

interface ItemCategory {
  id: string;
  code: string;
  name: string;
  description?: string;
  parentId?: string;
  isActive: boolean;
}

export default function ItemCategoriesPage() {
  const { loading: authLoading } = useAuth();
  const [items, setItems] = useState<ItemCategory[]>([]);
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
      const res = await fetch('/api/inventory/categories', { cache: 'no-store' });
      if (!res.ok) throw new Error('فشل التحميل');
      const data = await res.json();
      setItems(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'فشل التحميل'));
    } finally {
      setLoading(false);
    }
  };

  // Group by parent
  const roots = items.filter((c) => !c.parentId);
  const childrenOf = (parentId: string) => items.filter((c) => c.parentId === parentId);

  return (
    <div>
      <PageHeader
        title="📁 فئات الأصناف"
        description="التصنيف الهرمي للأصناف في المخزون"
        actions={
          <Link href="/admin/item-categories/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>فئة جديدة</Button>
          </Link>
        }
      />

      {error && <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4">{error}</div>}

      {loading ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
          <p className="mt-3 text-sm">جاري التحميل...</p>
        </div>
      ) : items.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">لا توجد فئات.</div>
      ) : (
        <div className="space-y-3">
          {roots.map((root) => {
            const children = childrenOf(root.id);
            return (
              <div key={root.id}>
                <Card accent="blue">
                  <div className="flex items-start justify-between">
                    <div>
                      <p className="text-xs text-gray-500 font-mono">{root.code}</p>
                      <h3 className="font-bold text-gray-800 mt-1">{root.name}</h3>
                      {root.description && <p className="text-sm text-gray-500 mt-1">{root.description}</p>}
                      <div className="mt-2 flex items-center gap-2">
                        <Badge variant={root.isActive ? 'success' : 'neutral'}>{root.isActive ? 'فعّال' : 'معطّل'}</Badge>
                        {children.length > 0 && <Badge variant="info">{children.length} فئة فرعية</Badge>}
                      </div>
                    </div>
                    <div className="flex items-center gap-1">
                      <EntityActions
                        editHref={`/admin/item-categories/${root.id}/edit`}
                        itemLabel={`${root.code} — ${root.name}`}
                        onDelete={async () => {
                          try {
                            await api.delete(`/api/inventory/categories/${root.id}`);
                            await load();
                          } catch (e) {
                            setError(getErrorMessage(e, 'فشل إيقاف التصنيف.'));
                          }
                        }}
                      />
                    </div>
                  </div>
                </Card>
                {children.length > 0 && (
                  <div className="ms-6 mt-2 space-y-2 border-s-2 border-gray-200 ps-4">
                    {children.map((c) => (
                      <Card key={c.id} accent="gray">
                        <div className="flex items-start justify-between">
                          <div>
                            <p className="text-xs text-gray-500 font-mono">{c.code}</p>
                            <h4 className="font-bold text-gray-800 text-sm">{c.name}</h4>
                            {c.description && <p className="text-xs text-gray-500 mt-1">{c.description}</p>}
                            <Badge variant={c.isActive ? 'success' : 'neutral'} className="mt-1">{c.isActive ? 'فعّال' : 'معطّل'}</Badge>
                          </div>
                          <EntityActions
                            editHref={`/admin/item-categories/${c.id}/edit`}
                            itemLabel={`${c.code} — ${c.name}`}
                            onDelete={async () => {
                              try {
                                await api.delete(`/api/inventory/categories/${c.id}`);
                                await load();
                              } catch (e) {
                                setError(getErrorMessage(e, 'فشل إيقاف التصنيف الفرعي.'));
                              }
                            }}
                          />
                        </div>
                      </Card>
                    ))}
                  </div>
                )}
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}
