'use client';

// Generic loading spinner used by loading.tsx files

import { Loader2 } from 'lucide-react';

export default function LoadingSpinner({ label = 'جاري التحميل...' }: { label?: string }) {
  return (
    <div className="flex flex-col items-center justify-center py-16 px-4" dir="rtl">
      <Loader2 className="h-12 w-12 text-blue-500 animate-spin" />
      <p className="mt-4 text-gray-500 text-sm">{label}</p>
    </div>
  );
}
