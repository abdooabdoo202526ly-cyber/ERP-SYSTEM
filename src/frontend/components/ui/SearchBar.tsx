'use client';

// SearchBar — مع debounce تلقائي (300ms).
// يقبل: value, onChange, placeholder

import { useEffect, useState } from 'react';
import { Search, X } from 'lucide-react';

export interface SearchBarProps {
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  /** تأخير debounce بـ ms (افتراضي 300) */
  debounceMs?: number;
  className?: string;
}

export function SearchBar({
  value,
  onChange,
  placeholder = 'بحث...',
  debounceMs = 300,
  className = '',
}: SearchBarProps) {
  const [local, setLocal] = useState(value);

  // Sync with external value changes (مثلاً clear filters)
  useEffect(() => {
    setLocal(value);
  }, [value]);

  // Debounce notify parent
  useEffect(() => {
    if (local === value) return;
    const t = setTimeout(() => onChange(local), debounceMs);
    return () => clearTimeout(t);
  }, [local, debounceMs, onChange, value]);

  return (
    <div className={`relative ${className}`}>
      <Search className="absolute top-1/2 -translate-y-1/2 start-3 h-4 w-4 text-gray-400 pointer-events-none" />
      <input
        type="text"
        value={local}
        onChange={(e) => setLocal(e.target.value)}
        placeholder={placeholder}
        className="w-full ps-9 pe-9 py-2 text-sm border border-gray-300 rounded-lg bg-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-200"
      />
      {local && (
        <button
          type="button"
          onClick={() => {
            setLocal('');
            onChange('');
          }}
          className="absolute top-1/2 -translate-y-1/2 end-2 p-1 text-gray-400 hover:text-gray-700"
          aria-label="مسح"
        >
          <X className="h-4 w-4" />
        </button>
      )}
    </div>
  );
}
