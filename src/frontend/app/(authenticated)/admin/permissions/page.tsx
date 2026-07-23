'use client';

// v1.0.32: صفحة كتالوج الصلاحيات (Permissions Catalog)
// عرض كل الصلاحيات المتاحة في النظام، مجمّعة بالفئة

import { useEffect, useState, useMemo } from 'react';
import { Shield, Key, Code2 } from 'lucide-react';
import { Card, Badge, PageHeader, SearchBar } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { identityApi, Permission, getErrorMessage } from '@/lib/api';

export default function PermissionsPage() {
  const { loading: authLoading } = useAuth();
  const [perms, setPerms] = useState<Permission[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [search, setSearch] = useState('');

  useEffect(() => {
    if (authLoading) return;
    load();
  }, [authLoading]);

  const load = async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await identityApi.listPermissions();
      setPerms(data);
    } catch (e: unknown) {
      setError(getErrorMessage(e, 'تعذّر تحميل الصلاحيات.'));
    } finally {
      setLoading(false);
    }
  };

  // Group by category
  const grouped = useMemo(() => {
    const q = search.trim().toLowerCase();
    const filtered = q
      ? perms.filter((p) =>
          p.code.toLowerCase().includes(q) ||
          p.nameAr.toLowerCase().includes(q) ||
          p.nameEn.toLowerCase().includes(q) ||
          p.category.toLowerCase().includes(q)
        )
      : perms;
    const map = new Map<string, Permission[]>();
    for (const p of filtered) {
      if (!map.has(p.category)) map.set(p.category, []);
      map.get(p.category)!.push(p);
    }
    return Array.from(map.entries()).sort(([a], [b]) => a.localeCompare(b));
  }, [perms, search]);

  const categoryColor = (cat: string) => {
    const colors: Record<string, string> = {
      Finance: 'bg-emerald-100 text-emerald-800 border-emerald-300',
      Sales: 'bg-blue-100 text-blue-800 border-blue-300',
      Procurement: 'bg-amber-100 text-amber-800 border-amber-300',
      Inventory: 'bg-purple-100 text-purple-800 border-purple-300',
      HR: 'bg-pink-100 text-pink-800 border-pink-300',
      Admin: 'bg-red-100 text-red-800 border-red-300',
    };
    return colors[cat] || 'bg-gray-100 text-gray-800 border-gray-300';
  };

  return (
    <div>
      <PageHeader
        title="🔐 كتالوج الصلاحيات (Permissions)"
        description="جميع الصلاحيات المتاحة في النظام — مجمّعة بالفئة"
      />

      <div className="mb-4 flex flex-wrap items-center gap-3">
        <SearchBar
          value={search}
          onChange={setSearch}
          placeholder="🔍 بحث (كود / اسم)..."
          className="flex-1 min-w-[280px] max-w-md"
        />
        <span className="text-sm text-gray-500">{perms.length} صلاحية</span>
      </div>

      {error && (
        <div className="bg-red-50 border border-red-200 text-red-700 px-4 py-3 rounded-lg mb-4 text-sm">{error}</div>
      )}

      {loading ? (
        <div className="text-center text-gray-500 py-8">جاري التحميل…</div>
      ) : (
        <div className="space-y-4">
          {grouped.map(([cat, list]) => (
            <Card key={cat} className={`border-2 ${categoryColor(cat)}`}>
              <div className="flex items-center gap-2 mb-3">
                <Shield className="h-5 w-5" />
                <h2 className="text-lg font-bold">{cat}</h2>
                <Badge variant="neutral">{list.length} صلاحية</Badge>
              </div>
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-2">
                {list.map((p) => (
                  <div key={p.code} className="bg-white rounded-md p-2 border border-gray-200 hover:shadow-sm transition-shadow">
                    <div className="flex items-start gap-2">
                      <Key className="h-3.5 w-3.5 text-gray-500 mt-0.5 flex-shrink-0" />
                      <div className="flex-1 min-w-0">
                        <div className="font-semibold text-sm text-gray-800">{p.nameAr}</div>
                        <div className="text-xs text-gray-500">{p.nameEn}</div>
                        <code className="text-[10px] font-mono bg-gray-100 px-1 py-0.5 rounded mt-1 inline-block">
                          <Code2 className="h-2.5 w-2.5 inline" /> {p.code}
                        </code>
                      </div>
                    </div>
                  </div>
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
