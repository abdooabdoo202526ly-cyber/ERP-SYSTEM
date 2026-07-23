'use client';

// Print Layout — قابل لإعادة الاستخدام لكل المستندات (فاتورة، سند قبض، قسيمة راتب).
// يُخفي الـ sidebar والـ header عند الطباعة.

import { useEffect } from 'react';

interface PrintLayoutProps {
  children: React.ReactNode;
  /** عنوان المستند يظهر في الـ browser tab */
  documentTitle?: string;
}

export function PrintLayout({ children, documentTitle }: PrintLayoutProps) {
  useEffect(() => {
    if (documentTitle) {
      document.title = documentTitle;
    }
    // أضف class على body لإخفاء الـ chrome (sidebar, header)
    document.body.classList.add('print-mode');
    return () => {
      document.body.classList.remove('print-mode');
    };
  }, [documentTitle]);

  return (
    <>
      {/* Print toolbar (يظهر على الشاشة فقط) */}
      <div className="no-print fixed top-0 left-0 right-0 z-50 bg-white border-b shadow-sm">
        <div className="max-w-4xl mx-auto px-4 py-3 flex items-center justify-between">
          <div className="flex items-center gap-2 text-sm text-gray-600">
            <span>🖨️</span>
            <span>{documentTitle || 'طباعة'}</span>
          </div>
          <div className="flex items-center gap-2">
            <button
              onClick={() => window.print()}
              className="bg-blue-600 hover:bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-semibold flex items-center gap-2"
            >
              🖨️ طباعة / حفظ PDF
            </button>
            <button
              onClick={() => window.history.back()}
              className="bg-gray-100 hover:bg-gray-200 text-gray-700 px-3 py-2 rounded-lg text-sm"
            >
              ← رجوع
            </button>
          </div>
        </div>
      </div>

      {/* Document body */}
      <div className="print-page pt-16 print:pt-0">
        {children}
      </div>

      {/* Print-specific CSS (load only on this page) */}
      <style jsx global>{`
        @page {
          size: A4;
          margin: 1.5cm 1.5cm 1.5cm 1.5cm;
        }
        @media print {
          body.print-mode {
            background: white !important;
          }
          body.print-mode .no-print {
            display: none !important;
          }
          body.print-mode .print-page {
            padding-top: 0 !important;
            margin: 0 !important;
            box-shadow: none !important;
          }
          body.print-mode * {
            -webkit-print-color-adjust: exact !important;
            print-color-adjust: exact !important;
          }
        }
        @media screen {
          .print-page {
            max-width: 210mm;
            min-height: 297mm;
            margin: 24px auto;
            padding: 32px;
            background: white;
            box-shadow: 0 4px 12px rgba(0, 0, 0, 0.08);
            border-radius: 4px;
          }
        }
      `}</style>
    </>
  );
}
