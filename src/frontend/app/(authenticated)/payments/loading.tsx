// Loading state لـ payments routes
import { PageHeader } from '@/components/ui';

export default function Loading() {
  return (
    <div>
      <PageHeader title="المدفوعات" description="جاري التحميل..." />
      <div className="space-y-2">
        <div className="h-10 bg-gray-100 rounded animate-pulse" />
        <div className="h-10 bg-gray-100 rounded animate-pulse" />
        <div className="h-10 bg-gray-100 rounded animate-pulse" />
      </div>
    </div>
  );
}
