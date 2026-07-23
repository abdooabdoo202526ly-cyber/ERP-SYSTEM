'use client';

// مكوّن حقل إدخال موحد (Input) — يدعم label, error, hint, iconLeft, iconRight

import { InputHTMLAttributes, forwardRef, ReactNode, useId } from 'react';
import { cn } from '@/lib/utils';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: ReactNode;
  error?: string;
  hint?: string;
  iconLeft?: ReactNode;
  iconRight?: ReactNode;
  containerClassName?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, error, hint, iconLeft, iconRight, containerClassName, className, id, ...props },
  ref
) {
  // FIX (v1.0.10): useId() بدلاً من Math.random() — كان يسبب hydration mismatch
  // لأن server و client بيولّدوا id مختلف في SSR.
  const reactId = useId();
  const inputId = id || `input-${reactId}`;
  return (
    <div className={cn('w-full', containerClassName)}>
      {label && (
        <label htmlFor={inputId} className="block text-sm font-medium text-gray-700 mb-1">
          {label}
        </label>
      )}
      <div className="relative">
        {iconLeft && (
          <span className="pointer-events-none absolute inset-y-0 right-0 flex items-center pr-3 text-gray-400">
            {iconLeft}
          </span>
        )}
        <input
          ref={ref}
          id={inputId}
          className={cn(
            'w-full rounded-lg border border-gray-300 bg-white px-3 py-2 text-sm',
            'focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200',
            'disabled:bg-gray-50 disabled:text-gray-400',
            error && 'border-red-400 focus:border-red-500 focus:ring-red-200',
            iconLeft && 'pr-10',
            iconRight && 'pl-10',
            className
          )}
          {...props}
        />
        {iconRight && (
          <span className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3 text-gray-400">
            {iconRight}
          </span>
        )}
      </div>
      {error ? (
        <p className="mt-1 text-xs text-red-600">{error}</p>
      ) : hint ? (
        <p className="mt-1 text-xs text-gray-500">{hint}</p>
      ) : null}
    </div>
  );
});
