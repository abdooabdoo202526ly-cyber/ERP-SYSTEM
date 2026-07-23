'use client';

// مكوّن Modal — نافذة منبثقة بسيطة
// الاستخدام: <Modal open={isOpen} onClose={...} title="...">...</Modal>

import { ReactNode, useEffect } from 'react';
import { X } from 'lucide-react';
import { cn } from '@/lib/utils';

export type ModalSize = 'sm' | 'md' | 'lg' | 'xl';

export interface ModalProps {
  open: boolean;
  onClose: () => void;
  title?: ReactNode;
  description?: ReactNode;
  children?: ReactNode;
  footer?: ReactNode;
  size?: ModalSize;
  /** إغلاق عند الضغط على backdrop — افتراضي true */
  closeOnBackdrop?: boolean;
}

const SIZE_STYLES: Record<ModalSize, string> = {
  sm: 'max-w-md',
  md: 'max-w-lg',
  lg: 'max-w-2xl',
  xl: 'max-w-4xl',
};

export function Modal({
  open,
  onClose,
  title,
  description,
  children,
  footer,
  size = 'md',
  closeOnBackdrop = true,
}: ModalProps) {
  // قفل scroll الـ body عندما الـ modal مفتوح
  useEffect(() => {
    if (open && typeof document !== 'undefined') {
      const prev = document.body.style.overflow;
      document.body.style.overflow = 'hidden';
      return () => {
        document.body.style.overflow = prev;
      };
    }
  }, [open]);

  // Escape للإغلاق
  useEffect(() => {
    if (!open) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    window.addEventListener('keydown', handler);
    return () => window.removeEventListener('keydown', handler);
  }, [open, onClose]);

  if (!open) return null;

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50"
      onClick={closeOnBackdrop ? onClose : undefined}
      dir="rtl"
    >
      <div
        className={cn(
          'w-full bg-white rounded-2xl shadow-xl max-h-[90vh] flex flex-col',
          SIZE_STYLES[size]
        )}
        onClick={(e) => e.stopPropagation()}
      >
        {(title || description) && (
          <div className="flex items-start justify-between px-6 py-4 border-b border-gray-100">
            <div>
              {title && <h2 className="text-lg font-bold text-gray-800">{title}</h2>}
              {description && <p className="text-sm text-gray-500 mt-1">{description}</p>}
            </div>
            <button
              onClick={onClose}
              className="text-gray-400 hover:text-gray-600 p-1 rounded-lg hover:bg-gray-100"
              aria-label="إغلاق"
            >
              <X className="h-5 w-5" />
            </button>
          </div>
        )}
        <div className="flex-1 overflow-y-auto px-6 py-4">{children}</div>
        {footer && (
          <div className="px-6 py-3 border-t border-gray-100 bg-gray-50 rounded-b-2xl flex items-center justify-end gap-2">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
