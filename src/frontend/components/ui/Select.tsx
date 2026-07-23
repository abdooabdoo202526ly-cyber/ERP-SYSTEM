'use client';

// مكوّن Select موحد — يلف native <select> مع label و error

import { SelectHTMLAttributes, forwardRef, ReactNode, useId } from 'react';
import { cn } from '@/lib/utils';

export interface SelectOption {
  label: string;
  value: string | number;
}

export interface SelectProps extends Omit<SelectHTMLAttributes<HTMLSelectElement>, 'children'> {
  label?: string;
  error?: string;
  hint?: string;
  options: SelectOption[];
  placeholder?: string;
  containerClassName?: string;
  children?: ReactNode;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(function Select(
  {
    label,
    error,
    hint,
    options,
    placeholder,
    containerClassName,
    className,
    id,
    value,
    ...props
  },
  ref
) {
  // FIX (v1.0.10): useId() بدلاً من Math.random() — كان يسبب hydration mismatch
  const reactId = useId();
  const selectId = id || `select-${reactId}`;
  return (
    <div className={cn('w-full', containerClassName)}>
      {label && (
        <label htmlFor={selectId} className="block text-sm font-medium text-gray-700 mb-1">
          {label}
        </label>
      )}
      <select
        ref={ref}
        id={selectId}
        value={value ?? ''}
        className={cn(
          'w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm',
          'focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200',
          'disabled:bg-gray-50 disabled:text-gray-400',
          error && 'border-red-400 focus:border-red-500 focus:ring-red-200',
          className
        )}
        {...props}
      >
        {placeholder !== undefined && (
          <option value="" disabled>
            {placeholder}
          </option>
        )}
        {options.map((opt) => (
          <option key={opt.value} value={opt.value}>
            {opt.label}
          </option>
        ))}
      </select>
      {error ? (
        <p className="mt-1 text-xs text-red-600">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-gray-500">{hint}</p>
      ) : null}
    </div>
  );
});
