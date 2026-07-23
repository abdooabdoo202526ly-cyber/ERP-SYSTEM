'use client';

// مكوّن Table — جدول موحد مع loading / empty states
// الاستخدام: <Table columns={...} data={...} loading={...} emptyMessage="..." />

import { ReactNode } from 'react';
import Link from 'next/link';
import { cn } from '@/lib/utils';

export interface TableColumn<T> {
  key: string;
  header: ReactNode;
  /** الـ cell renderer — يُرجع ReactNode */
  render: (row: T) => ReactNode;
  /** عرض العمود (Tailwind class) — اختياري */
  className?: string;
  /** محاذاة الـ header — افتراضي يمين في RTL */
  align?: 'start' | 'center' | 'end';
}

export interface TableProps<T> {
  columns: TableColumn<T>[];
  data: T[];
  loading?: boolean;
  emptyMessage?: ReactNode;
  rowKey: (row: T) => string;
  onRowClick?: (row: T) => void;
  /** DEC-031: If set, each row becomes a link to this URL. Mutually exclusive with onRowClick. */
  rowHref?: (row: T) => string;
  className?: string;
}

export function Table<T>({
  columns,
  data,
  loading = false,
  emptyMessage = 'لا توجد بيانات',
  rowKey,
  onRowClick,
  rowHref,
  className,
}: TableProps<T>) {
  if (loading) {
    return (
      <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
        <div className="inline-block h-8 w-8 animate-spin rounded-full border-4 border-blue-500 border-r-transparent" />
        <p className="mt-3 text-sm">جاري التحميل...</p>
      </div>
    );
  }

  if (data.length === 0) {
    return (
      <div className="bg-white rounded-xl shadow-sm p-12 text-center text-gray-500">
        {emptyMessage}
      </div>
    );
  }

  return (
    <div className={cn('bg-white rounded-xl shadow-sm overflow-hidden', className)}>
      <div className="overflow-x-auto">
        <table className="w-full">
          <thead className="bg-gray-50 border-b border-gray-200">
            <tr>
              {columns.map((col) => (
                <th
                  key={col.key}
                  className={cn(
                    'px-4 py-3 text-xs font-semibold text-gray-600',
                    col.align === 'center' && 'text-center',
                    col.align === 'end' && 'text-end',
                    (!col.align || col.align === 'start') && 'text-start',
                    col.className
                  )}
                >
                  {col.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row) => {
              const href = rowHref ? rowHref(row) : null;
              const inner = (
                <>
                  {columns.map((col) => (
                    <td
                      key={col.key}
                      className={cn(
                        'px-4 py-3 text-sm',
                        col.align === 'center' && 'text-center',
                        col.align === 'end' && 'text-end',
                        col.className
                      )}
                    >
                      {col.render(row)}
                    </td>
                  ))}
                </>
              );
              return (
                <tr
                  key={rowKey(row)}
                  onClick={onRowClick ? () => onRowClick(row) : undefined}
                  className={cn(
                    'border-b border-gray-100 last:border-0',
                    (onRowClick || rowHref) && 'cursor-pointer hover:bg-gray-50'
                  )}
                >
                  {href ? (
                    <td colSpan={columns.length} className="p-0">
                      <Link href={href} className="block px-4 py-3 text-sm no-underline text-inherit">
                        <div className="grid" style={{ gridTemplateColumns: `repeat(${columns.length}, minmax(0, 1fr))` }}>
                          {columns.map((col) => (
                            <div
                              key={col.key}
                              className={cn(
                                col.align === 'center' && 'text-center',
                                col.align === 'end' && 'text-end',
                                col.className
                              )}
                            >
                              {col.render(row)}
                            </div>
                          ))}
                        </div>
                      </Link>
                    </td>
                  ) : (
                    inner
                  )}
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
