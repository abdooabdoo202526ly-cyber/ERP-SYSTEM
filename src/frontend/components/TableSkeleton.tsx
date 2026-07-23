'use client';

// Table skeleton for list pages loading state

export default function TableSkeleton({ rows = 5 }: { rows?: number }) {
  return (
    <div className="space-y-3" dir="rtl">
      {/* Page header skeleton */}
      <div className="bg-white rounded-xl shadow-sm p-6">
        <div className="h-7 w-48 bg-gray-200 rounded animate-pulse mb-2" />
        <div className="h-4 w-64 bg-gray-100 rounded animate-pulse" />
      </div>
      {/* Table skeleton */}
      <div className="bg-white rounded-xl shadow-sm p-4 space-y-3">
        {Array.from({ length: rows }).map((_, i) => (
          <div key={i} className="flex items-center gap-4 py-3 border-b border-gray-100 last:border-0">
            <div className="h-4 w-32 bg-gray-200 rounded animate-pulse" />
            <div className="h-4 w-48 bg-gray-100 rounded animate-pulse flex-1" />
            <div className="h-4 w-24 bg-gray-200 rounded animate-pulse" />
            <div className="h-8 w-8 bg-gray-100 rounded animate-pulse" />
          </div>
        ))}
      </div>
    </div>
  );
}
