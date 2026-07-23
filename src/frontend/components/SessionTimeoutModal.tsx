'use client';

// SessionTimeoutModal — DL 71: Modal shown 1 min before JWT expiry

'use client';

import { useSessionTimeout } from '@/lib/useSessionTimeout';
import { Clock, RefreshCw, LogOut } from 'lucide-react';

export default function SessionTimeoutModal() {
  const { showWarning, secondsRemaining, refresh, logout } = useSessionTimeout();

  if (!showWarning) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50" dir="rtl">
      <div className="bg-white rounded-2xl shadow-2xl p-6 max-w-md w-full mx-4">
        <div className="flex items-center gap-3 mb-4">
          <div className="bg-yellow-100 p-3 rounded-full">
            <Clock className="h-6 w-6 text-yellow-600" />
          </div>
          <div>
            <h2 className="text-xl font-bold text-gray-800">الجلسة على وشك الانتهاء</h2>
            <p className="text-sm text-gray-500 mt-1">
              ستنتهي جلستك بعد <span className="font-mono font-bold text-yellow-600">{secondsRemaining}</span> ثانية
            </p>
          </div>
        </div>

        <p className="text-gray-600 text-sm mb-6">
          هل تريد البقاء مسجلاً؟ سيتم تمديد جلستك تلقائياً.
        </p>

        <div className="flex items-center gap-2 justify-end">
          <button
            onClick={logout}
            className="flex items-center gap-2 px-4 py-2 rounded-lg text-gray-600 hover:bg-gray-100 text-sm"
          >
            <LogOut className="h-4 w-4" />
            تسجيل الخروج
          </button>
          <button
            onClick={refresh}
            className="flex items-center gap-2 px-4 py-2 rounded-lg bg-blue-600 text-white hover:bg-blue-700 text-sm"
          >
            <RefreshCw className="h-4 w-4" />
            ابقني مسجلاً
          </button>
        </div>
      </div>
    </div>
  );
}