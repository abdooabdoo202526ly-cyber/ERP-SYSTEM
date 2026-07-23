using System;
using System.Collections.Generic;

namespace ERPSystem.Modules.Finance.Entities;

/// <summary>
/// نوع حدث الـ Event Bus الذي يستقبله الـ Rules Engine.
/// مبدئياً نغطي StockReceived (من Inventory) — لكنه قابل للتوسعة.
/// </summary>
public enum TriggeringEvent
{
    StockReceived = 1,
    StockIssued = 2,
    InvoiceCreated = 3,
    PaymentReceived = 4,
}

/// <summary>
/// محرك القواعد (Rules Engine) — MVP.
///
/// كل قاعدة تقول: "عند ورود حدث X بقيم Y، أنشئ Journal Entry بهذه السطور".
/// حالياً: قواعيد بسيطة (1 حدث → N سطور debit/credit).
/// مستقبلياً: لوغين متقدم (شروط، حسابات، إلخ).
/// </summary>
public class PostingRule
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public TriggeringEvent EventType { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>قالب الـ Journal Entry المُنشأ عند تفعيل القاعدة (JSON).</summary>
    public string TemplateJson { get; set; } = "{}";

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// قالب القيد داخل PostingRule.TemplateJson.
/// مصمّم كـ POCO ليكون typed عند الـ deserialization.
/// </summary>
public class PostingRuleTemplate
{
    public string Description { get; set; } = string.Empty;
    public string? Reference { get; set; }

    /// <summary>السطور: كل سطر يقول debit/credit + الـ account code (يحلّ لـ ID وقت التشغيل)</summary>
    public List<PostingRuleLineTemplate> Lines { get; set; } = new();
}

public class PostingRuleLineTemplate
{
    /// <summary>كود الحساب (Account.Code) — يُحلّ لـ AccountId وقت التشغيل</summary>
    public string AccountCode { get; set; } = string.Empty;

    /// <summary>نوع الحركة: "debit" أو "credit"</summary>
    public string Side { get; set; } = "debit";

    /// <summary>صيغة المبلغ — يدعم متغيرات من الحدث (مثال: "{amount}")</summary>
    public string AmountFormula { get; set; } = "0";
}
