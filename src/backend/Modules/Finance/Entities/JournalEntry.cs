using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Finance.Entities;

/// <summary>
/// حالة القيد المحاسبي
/// - Draft: قيد مُعدّ، قابل للتعديل، لا يؤثر على الأرصدة
/// - Posted: مُرحّل، مقفل، أثّر على General Ledger
/// - Reversed: مُعكّس بقيد آخر (idempotent reversal)
/// </summary>
public enum JournalEntryStatus
{
    Draft = 1,
    Posted = 2,
    Reversed = 3,
}

/// <summary>
/// القيد المحاسبي (Journal Entry / Voucher).
///
/// كل قيد يجب أن يحقق معادلة الـ Double-Entry:
///   مجموع المدين (Σ debit) == مجموع الدائن (Σ credit)
///
/// على مستوى الـ DB: نخزّن كل سطر بحقل debit و credit منفصلين
/// (لا نخزّن المبلغ بإشارة — هذا أصدق محاسبياً ويسهّل التقارير).
/// </summary>
public class JournalEntry
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    /// <summary>رقم القيد التسلسلي داخل المستأجر (مثال: "JE-2026-0001")</summary>
    public string EntryNumber { get; set; } = string.Empty;

    public DateTime EntryDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; } // مرجع خارجي (رقم فاتورة، إلخ)

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    /// <summary>المستخدم الذي أنشأ القيد</summary>
    public Guid CreatedByUserId { get; set; }

    /// <summary>تاريخ الترحيل (null إذا draft)</summary>
    public DateTime? PostedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public ICollection<JournalLine> Lines { get; set; } = new List<JournalLine>();
}

/// <summary>
/// سطر القيد المحاسبي — يمثل حركة على حساب واحد.
/// إما Debit > 0 و Credit = 0، أو العكس. لا يُسمح بكليهما أو لا واحد.
/// </summary>
public class JournalLine
{
    public Guid Id { get; set; }
    public Guid JournalEntryId { get; set; }
    public Guid AccountId { get; set; }

    public decimal Debit { get; set; }
    public decimal Credit { get; set; }

    public string? Description { get; set; }
    public int LineNumber { get; set; } // ترتيب السطر في القيد

    // Navigation
    public JournalEntry? JournalEntry { get; set; }
    public Account? Account { get; set; }
}
