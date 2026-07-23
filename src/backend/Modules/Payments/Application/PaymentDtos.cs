using System;
using System.Collections.Generic;
using ERPSystem.Modules.Payments.Entities;

namespace ERPSystem.Modules.Payments.Application;

// ============== Request DTOs ==============

/// <summary>طلب إنشاء Payment جديد (في حالة Draft).</summary>
public sealed class CreatePaymentRequest
{
    /// <summary>"Customer" أو "Vendor".</summary>
    public string PartyType { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";

    /// <summary>"Cash" | "Bank" | "Transfer" | "Check".</summary>
    public string PaymentMethod { get; set; } = PaymentMethods.Cash;

    public Guid? BankAccountId { get; set; }
    public string? Notes { get; set; }

    /// <summary>تخصيصات (اختيارية — يمكن إضافتها بعد Post أيضاً).</summary>
    public List<CreatePaymentAllocationRequest> Allocations { get; set; } = new();
}

/// <summary>بند تخصيص دفعة على فاتورة (VendorBill أو SalesInvoice مستقبلي).</summary>
public sealed class CreatePaymentAllocationRequest
{
    /// <summary>"SalesInvoice" أو "VendorBill".</summary>
    public string RefType { get; set; } = string.Empty;
    public Guid RefId { get; set; }
    public decimal AmountApplied { get; set; }
}

/// <summary>طلب تخصيص دفعة بعد Post (يُضيف allocation جديد).</summary>
public sealed class AllocatePaymentRequest
{
    public List<CreatePaymentAllocationRequest> Allocations { get; set; } = new();
}

// ============== Response DTOs ==============

public sealed class PaymentAllocationResponse
{
    public Guid Id { get; set; }
    public string RefType { get; set; } = string.Empty;
    public Guid RefId { get; set; }
    public decimal AmountApplied { get; set; }
}

public sealed class PaymentResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string PartyType { get; set; } = string.Empty;
    public Guid PartyId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public string PaymentMethod { get; set; } = string.Empty;
    public Guid? BankAccountId { get; set; }
    public string? Notes { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<PaymentAllocationResponse> Allocations { get; set; } = new();
    public decimal AllocatedAmount => Allocations.Sum(a => a.AmountApplied);
    public decimal OnAccountAmount => Amount - AllocatedAmount;
}

// ============== Result Pattern ==============

public sealed class PaymentResult<T>
{
    public bool Succeeded { get; init; }
    public T? Value { get; init; }
    public string? Error { get; init; }
    public PaymentErrorCode? ErrorCode { get; init; }

    public static PaymentResult<T> Ok(T value) => new() { Succeeded = true, Value = value };
    public static PaymentResult<T> Fail(string error, PaymentErrorCode code) =>
        new() { Succeeded = false, Error = error, ErrorCode = code };
}

public enum PaymentErrorCode
{
    NotFound,
    ValidationError,
    NotFoundParty,          // المورّد/العميل غير موجود
    NotFoundRef,            // الفاتورة المُحال إليها غير موجودة
    InvalidStatus,          // لا يمكن الترحيل/التخصيص في هذه الحالة
    OverAllocation,         // sum(allocations) > Amount
    MissingAllocation,      // يجب تخصيص كل المبلغ (مستقبلي — نسمح بـ On Account)
    Unbalanced,             // القيد المحاسبي غير متوازن
    TenantMismatch,
    Internal
}
