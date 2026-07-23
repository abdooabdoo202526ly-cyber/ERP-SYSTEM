'use client';

// مكوّن Card — container للـ widgets / KPIs / القوائم

import { HTMLAttributes, ReactNode } from 'react';
import { cn } from '@/lib/utils';

export interface CardProps extends Omit<HTMLAttributes<HTMLDivElement>, 'title'> {
  title?: ReactNode;
  description?: ReactNode;
  actions?: ReactNode;
  footer?: ReactNode;
  /** لون الحد الأيمن (RTL) — يعطي تمييز بصري للـ category */
  accent?: 'blue' | 'green' | 'purple' | 'yellow' | 'red' | 'gray' | 'none';
}

const ACCENT_STYLES: Record<NonNullable<CardProps['accent']>, string> = {
  blue: 'border-r-4 border-blue-500',
  green: 'border-r-4 border-green-500',
  purple: 'border-r-4 border-purple-500',
  yellow: 'border-r-4 border-yellow-500',
  red: 'border-r-4 border-red-500',
  gray: 'border-r-4 border-gray-300',
  none: '',
};

export function Card({
  title,
  description,
  actions,
  footer,
  accent = 'none',
  className,
  children,
  ...props
}: CardProps) {
  return (
    <div
      className={cn(
        'bg-white rounded-xl shadow-sm border border-gray-100',
        ACCENT_STYLES[accent],
        className
      )}
      {...props}
    >
      {(title || actions) && (
        <div className="flex items-center justify-between px-5 py-4 border-b border-gray-100">
          <div>
            {title && <h3 className="font-bold text-gray-800">{title}</h3>}
            {description && <p className="text-sm text-gray-500 mt-0.5">{description}</p>}
          </div>
          {actions && <div className="flex items-center gap-2">{actions}</div>}
        </div>
      )}
      <div className={cn('p-5', footer && 'pb-0')}>{children}</div>
      {footer && (
        <div className="px-5 py-3 border-t border-gray-100 bg-gray-50 rounded-b-xl">
          {footer}
        </div>
      )}
    </div>
  );
}
