'use client';

// مكوّن زر موحد (Button) — Tailwind فقط، بدون مكتبات خارجية
// الـ variants: primary | secondary | danger | ghost | outline
// الـ sizes: sm | md | lg

import { ButtonHTMLAttributes, forwardRef } from 'react';
import { Loader2 } from 'lucide-react';
import { cn } from '@/lib/utils';

export type ButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost' | 'outline';
export type ButtonSize = 'sm' | 'md' | 'lg';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  loading?: boolean;
  iconLeft?: React.ReactNode;
  iconRight?: React.ReactNode;
  fullWidth?: boolean;
}

const VARIANT_STYLES: Record<ButtonVariant, string> = {
  primary: 'bg-blue-600 text-white hover:bg-blue-700 focus:ring-blue-500 disabled:bg-blue-300',
  secondary: 'bg-gray-100 text-gray-800 hover:bg-gray-200 focus:ring-gray-400 disabled:bg-gray-50 disabled:text-gray-400',
  danger: 'bg-red-600 text-white hover:bg-red-700 focus:ring-red-500 disabled:bg-red-300',
  ghost: 'bg-transparent text-gray-700 hover:bg-gray-100 focus:ring-gray-300',
  outline: 'bg-white text-gray-700 border border-gray-300 hover:bg-gray-50 focus:ring-blue-400',
};

const SIZE_STYLES: Record<ButtonSize, string> = {
  sm: 'h-8 px-3 text-xs',
  md: 'h-10 px-4 text-sm',
  lg: 'h-12 px-6 text-base',
};

export const Button = forwardRef<HTMLButtonElement, ButtonProps>(function Button(
  {
    variant = 'primary',
    size = 'md',
    loading = false,
    disabled,
    iconLeft,
    iconRight,
    fullWidth = false,
    className,
    children,
    ...props
  },
  ref
) {
  return (
    <button
      ref={ref}
      disabled={disabled || loading}
      className={cn(
        // الأساسيات
        'inline-flex items-center justify-center gap-2 rounded-lg font-semibold transition-colors',
        'focus:outline-none focus:ring-2 focus:ring-offset-1',
        'disabled:cursor-not-allowed',
        // variant
        VARIANT_STYLES[variant],
        // size
        SIZE_STYLES[size],
        // full width
        fullWidth && 'w-full',
        className
      )}
      {...props}
    >
      {loading ? (
        <Loader2 className="h-4 w-4 animate-spin" />
      ) : (
        iconLeft && <span className="inline-flex">{iconLeft}</span>
      )}
      <span>{children}</span>
      {!loading && iconRight && <span className="inline-flex">{iconRight}</span>}
    </button>
  );
});
