// مساعدات دمج الـ Tailwind classes
// clsx + tailwind-merge يُزيل التعارضات (مثلاً px-2 px-4 → px-2)

import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}

// === Date/Time formatters ===
// نستخدم 'en-GB' locale لإظهار التاريخ الميلادي بالأرقام الإنجليزية (DD/MM/YYYY)
// تجنّب 'ar-EG' لأنّه يُحوّل إلى تقويم هجري/أرقام عربية بحسب بيئة المتصفح.

/** تنسيق تاريخ ميلادي. يُرجع '—' لو فارغ/غير صالح. */
export function formatDate(value?: string | null | Date): string {
  if (!value) return '—';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleDateString('en-GB', { day: '2-digit', month: '2-digit', year: 'numeric' });
}

/** تنسيق وقت (HH:MM). */
export function formatTime(value?: string | null | Date): string {
  if (!value) return '—';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' });
}

/** تنسيق تاريخ + وقت. */
export function formatDateTime(value?: string | null | Date): string {
  if (!value) return '—';
  const d = new Date(value);
  if (isNaN(d.getTime())) return '—';
  return d.toLocaleString('en-GB', {
    day: '2-digit', month: '2-digit', year: 'numeric',
    hour: '2-digit', minute: '2-digit'
  });
}
