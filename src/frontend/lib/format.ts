// Helpers لتنسيق الأرقام بالعربية locale (لكن أرقام إنجليزية)
//
// ملاحظة: الـ convention المعتمد هو 'en-GB' لأرقام إنجليزية مع dd/MM/yyyy.
// للأرقام المالية، نستخدم أيضاً toLocaleString('en-GB', ...) ليبقى الرقم بفواصل إنجليزية (1,234.56).

/**
 * تنسيق رقم بمقاسات الآلاف والأرقام العشرية.
 * @example formatNumber(1234.5678) => "1,234.5678"
 */
export function formatNumber(value: number | null | undefined, fractionDigits = 4): string {
  if (value === null || value === undefined || isNaN(value)) return '—';
  return new Intl.NumberFormat('en-GB', {
    minimumFractionDigits: 0,
    maximumFractionDigits: fractionDigits,
  }).format(value);
}

/**
 * تنسيق مبلغ مالي (عملة).
 * @example formatMoney(1234.5, 'LYD') => "1,234.5000 LYD"
 */
export function formatMoney(value: number | null | undefined, currency = 'LYD', fractionDigits = 4): string {
  if (value === null || value === undefined || isNaN(value)) return '—';
  return `${formatNumber(value, fractionDigits)} ${currency}`;
}
