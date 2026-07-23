'use client';

// Pagination — قابل لإعادة الاستخدام في كل الـ lists.
// يقبل: total, page, pageSize, onPageChange
// يعرض: First/Prev/Page numbers/Next/Last + JumpToPage + PageSizeSelector

import { ChevronFirst, ChevronLast, ChevronLeft, ChevronRight } from 'lucide-react';

export interface PaginationProps {
  /** إجمالي عدد السجلات */
  total: number;
  /** رقم الصفحة الحالية (1-based) */
  page: number;
  /** حجم الصفحة (افتراضي 25) */
  pageSize?: number;
  /** عدد أرقام الصفحات المعروضة */
  windowSize?: number;
  /** عند تغيير الصفحة */
  onPageChange: (page: number) => void;
  /** عند تغيير حجم الصفحة */
  onPageSizeChange?: (size: number) => void;
  /** خيارات حجم الصفحة */
  pageSizeOptions?: number[];
}

export function Pagination({
  total,
  page,
  pageSize = 25,
  windowSize = 5,
  onPageChange,
  onPageSizeChange,
  pageSizeOptions = [10, 25, 50, 100],
}: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const current = Math.min(Math.max(1, page), totalPages);
  const start = (current - 1) * pageSize + 1;
  const end = Math.min(current * pageSize, total);
  if (total === 0) return null;

  // احسب أرقام الصفحات
  const half = Math.floor(windowSize / 2);
  let from = Math.max(1, current - half);
  let to = Math.min(totalPages, from + windowSize - 1);
  if (to - from + 1 < windowSize) {
    from = Math.max(1, to - windowSize + 1);
  }
  const pages: number[] = [];
  for (let i = from; i <= to; i++) pages.push(i);

  return (
    <div className="flex flex-wrap items-center justify-between gap-3 py-3 px-2 text-sm">
      {/* Left: info */}
      <div className="text-gray-600">
        عرض <span className="font-bold font-mono">{start}</span>–<span className="font-bold font-mono">{end}</span> من{' '}
        <span className="font-bold font-mono">{total}</span>
      </div>

      {/* Center: page numbers */}
      <div className="flex items-center gap-1">
        <button
          onClick={() => onPageChange(1)}
          disabled={current === 1}
          className="h-8 w-8 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center"
          aria-label="الصفحة الأولى"
        >
          <ChevronFirst className="h-4 w-4" />
        </button>
        <button
          onClick={() => onPageChange(current - 1)}
          disabled={current === 1}
          className="h-8 w-8 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center"
          aria-label="السابق"
        >
          <ChevronLeft className="h-4 w-4" />
        </button>
        {from > 1 && <span className="px-1 text-gray-400">…</span>}
        {pages.map((p) => (
          <button
            key={p}
            onClick={() => onPageChange(p)}
            className={`h-8 min-w-8 px-2 rounded text-sm font-mono ${
              p === current
                ? 'bg-blue-600 text-white'
                : 'border border-gray-300 bg-white hover:bg-gray-50'
            }`}
          >
            {p}
          </button>
        ))}
        {to < totalPages && <span className="px-1 text-gray-400">…</span>}
        <button
          onClick={() => onPageChange(current + 1)}
          disabled={current === totalPages}
          className="h-8 w-8 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center"
          aria-label="التالي"
        >
          <ChevronRight className="h-4 w-4" />
        </button>
        <button
          onClick={() => onPageChange(totalPages)}
          disabled={current === totalPages}
          className="h-8 w-8 rounded border border-gray-300 bg-white hover:bg-gray-50 disabled:opacity-40 disabled:cursor-not-allowed flex items-center justify-center"
          aria-label="الصفحة الأخيرة"
        >
          <ChevronLast className="h-4 w-4" />
        </button>
      </div>

      {/* Right: page size */}
      {onPageSizeChange && (
        <div className="flex items-center gap-2 text-gray-600">
          <span>حجم الصفحة:</span>
          <select
            value={pageSize}
            onChange={(e) => onPageSizeChange(Number(e.target.value))}
            className="border border-gray-300 rounded px-2 py-1 text-sm bg-white"
          >
            {pageSizeOptions.map((s) => (
              <option key={s} value={s}>{s}</option>
            ))}
          </select>
        </div>
      )}
    </div>
  );
}
