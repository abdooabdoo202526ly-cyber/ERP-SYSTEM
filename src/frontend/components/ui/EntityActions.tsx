'use client';

// v1.0.23: Reusable action buttons (edit + delete) for any list page.
// The component handles the confirm dialog and the API call via the
// provided onDelete callback. Used across the entire ERP to enforce
// consistent UX and to avoid 18 duplicate implementations.

import { useState } from 'react';
import Link from 'next/link';
import { Pencil, Trash2, Loader2 } from 'lucide-react';
import { Button } from './Button';

export interface EntityActionsProps {
  // Edit target (required if hideEdit is not true)
  editHref?: string;
  // Label shown in the delete confirm dialog, e.g. "حساب النقدية"
  itemLabel: string;
  // Async delete function that throws on failure
  onDelete: () => Promise<void>;
  // Optional: disable delete (e.g. when item is in use)
  disableDelete?: boolean;
  // Optional: hide the edit button
  hideEdit?: boolean;
  // Optional: hide the delete button
  hideDelete?: boolean;
}

export function EntityActions({
  editHref,
  itemLabel,
  onDelete,
  disableDelete = false,
  hideEdit = false,
  hideDelete = false,
}: EntityActionsProps) {
  const [deleting, setDeleting] = useState(false);

  const handleDelete = async () => {
    if (disableDelete) return;
    if (!window.confirm(`هل تريد إلغاء تفعيل "${itemLabel}"؟\n(سيتم إخفاءه من القوائم النشطة فقط)`)) {
      return;
    }
    setDeleting(true);
    try {
      await onDelete();
    } finally {
      setDeleting(false);
    }
  };

  return (
    <div className="flex items-center justify-center gap-1">
      {!hideEdit && (
        <Link href={editHref} title="تعديل">
          <Button variant="ghost" size="sm" iconLeft={<Pencil className="h-3 w-3" />} />
        </Link>
      )}
      {!hideDelete && (
        <button
          type="button"
          onClick={handleDelete}
          disabled={disableDelete || deleting}
          className="text-red-500 hover:text-red-700 p-1.5 rounded hover:bg-red-50 disabled:opacity-50 disabled:cursor-not-allowed"
          title={disableDelete ? 'لا يمكن الحذف' : 'إلغاء التفعيل'}
        >
          {deleting ? (
            <Loader2 className="h-3.5 w-3.5 animate-spin" />
          ) : (
            <Trash2 className="h-3.5 w-3.5" />
          )}
        </button>
      )}
    </div>
  );
}
