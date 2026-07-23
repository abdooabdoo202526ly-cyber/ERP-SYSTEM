'use client';

// صفحة المشاريع (Projects) — قائمة

import { useEffect, useState } from 'react';
import Link from 'next/link';
import { Plus, Pencil } from 'lucide-react';
import { formatDate, formatTime } from '@/lib/utils';
import { Card, Badge, PageHeader, Button, EntityActions } from '@/components/ui';
import { useAuth } from '@/lib/useAuth';
import { projectsApi, Project, getErrorMessage, PROJECT_STATUSES } from '@/lib/api';

export default function ProjectsPage() {
  const { loading: authLoading } = useAuth();
  const [projects, setProjects] = useState<Project[]>([]);
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
      const data = await projectsApi.listProjects();
      setProjects(data);
    } catch (e: unknown) {
      const err = e as { response?: { data?: { detail?: string } } };
      setError(err?.response?.data?.detail || 'فشل التحميل');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div>
      <PageHeader
        title="📊 المشاريع"
        description="قائمة المشاريع النشطة والمكتملة"
        actions={
          <Link href="/projects/new">
            <Button variant="primary" iconLeft={<Plus className="h-4 w-4" />}>
              مشروع جديد
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
      ) : projects.length === 0 ? (
        <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
          لا توجد مشاريع في هذا الـ tenant.
        </div>
      ) : (
        <div className="space-y-3">
          {projects.map((p) => (
            <Card key={p.id} accent="purple">
              <div className="flex items-start justify-between">
                <div>
                  <p className="text-xs text-gray-500 font-mono">{p.code}</p>
                  <h3 className="font-bold text-gray-800 mt-1 text-lg">{p.name}</h3>
                  {p.description && <p className="text-sm text-gray-500 mt-1">{p.description}</p>}
                </div>
                <div className="flex items-center gap-1">
                  <EntityActions
                    editHref={`/projects/${p.id}/edit`}
                    itemLabel={`${p.code} — ${p.name}`}
                    onDelete={async () => {
                      try {
                        await projectsApi.deleteProject(p.id);
                        await load();
                      } catch (e) {
                        setError(getErrorMessage(e, 'فشل إلغاء التفعيل.'));
                      }
                    }}
                  />
                  <Badge variant="info">{PROJECT_STATUSES[p.status] || p.status}</Badge>
                </div>
              </div>
              <div className="mt-3 pt-3 border-t grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-gray-500">الميزانية</p>
                  <p className="font-bold text-gray-800">{p.budget?.toLocaleString()}</p>
                </div>
                <div>
                  <p className="text-gray-500">تاريخ البدء</p>
                  <p className="font-mono text-gray-800">
                    {formatDate(p.startDate)}
                  </p>
                </div>
              </div>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}


