using System;
using System.Collections.Generic;
using ERPSystem.Modules.AccountsReceivable.Entities;

namespace ERPSystem.Modules.AccountsReceivable.Application;

// ============== Customer DTOs ==============

public sealed class CreateCustomerRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? CompanyId { get; set; } // v1.0.11: optional - many tenants use single-company mode
    public string? NameEn { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
}

public sealed class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; } = 30;
    public bool IsActive { get; set; } = true;
}

public sealed class CustomerResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public decimal? CreditLimit { get; set; }
    public int PaymentTermsDays { get; set; }
    public bool IsActive { get; set; }
}

// ============== SalesInvoice DTOs ==============

public sealed class CreateSalesInvoiceLineRequest
{
    public string Description { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class CreateSalesInvoiceRequest
{
    public Guid CustomerId { get; set; }
    public Guid? CompanyId { get; set; } // v1.0.11: optional
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Notes { get; set; }
    public Guid? ProjectId { get; set; }
    public List<CreateSalesInvoiceLineRequest> Lines { get; set; } = new();
    /// <summary>إذا true، يتم ترحيل الفاتورة بعد إنشائها (Dr 1230 / Cr 5110 + Status=Sent).</summary>
    public bool PostImmediately { get; set; } = false;
}

public sealed class UpdateSalesInvoiceRequest
{
    public DateTime? DueDate { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public decimal ExchangeRate { get; set; } = 1m;
    public string? Notes { get; set; }
    public Guid? ProjectId { get; set; }
    public List<CreateSalesInvoiceLineRequest> Lines { get; set; } = new();
}

public sealed class SalesInvoiceLineResponse
{
    public Guid Id { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal LineTotal { get; set; }
}

public sealed class SalesInvoiceResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public decimal ExchangeRate { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public decimal Outstanding { get; set; }
    public SalesInvoiceStatus Status { get; set; }
    public string? Notes { get; set; }
    public Guid? ProjectId { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SalesInvoiceLineResponse> Lines { get; set; } = new();
    public List<ReceiptAllocationResponse> Allocations { get; set; } = new();
}

// ============== Receipt DTOs ==============

public sealed class CreateReceiptAllocationRequest
{
    public Guid SalesInvoiceId { get; set; }
    public decimal AmountApplied { get; set; }
}

public sealed class CreateReceiptRequest
{
    public Guid CustomerId { get; set; }
    public Guid? CompanyId { get; set; } // v1.0.11: optional
    public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public List<CreateReceiptAllocationRequest> Allocations { get; set; } = new();
    /// <summary>إذا true، يتم ترحيل السند بعد إنشائه (Dr 1210 / Cr 1230 + Status=Posted).</summary>
    public bool PostImmediately { get; set; } = false;
}

public sealed class ReceiptAllocationResponse
{
    public Guid Id { get; set; }
    public Guid SalesInvoiceId { get; set; }
    public string? SalesInvoiceNumber { get; set; }
    public decimal AmountApplied { get; set; }
}

public sealed class ReceiptResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public decimal Amount { get; set; }
    public string CurrencyCode { get; set; } = "LYD";
    public string? PaymentMethod { get; set; }
    public string? Notes { get; set; }
    public DateTime? PostedAt { get; set; }
    public Guid? PostedBy { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<ReceiptAllocationResponse> Allocations { get; set; } = new();
}

// ============== Aging AR DTO ==============

public sealed class ArAgingBucket
{
    public decimal Bucket0To30 { get; set; }
    public decimal Bucket31To60 { get; set; }
    public decimal Bucket61To90 { get; set; }
    public decimal Bucket91To120 { get; set; }
    public decimal Bucket120Plus { get; set; }
    public decimal Total { get; set; }
}

public sealed class ArAgingRow
{
    public Guid CustomerId { get; set; }
    public string CustomerCode { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public ArAgingBucket Buckets { get; set; } = new();
}

public sealed class ArAgingReport
{
    public DateTime AsOfDate { get; set; }
    public List<ArAgingRow> Rows { get; set; } = new();
    public ArAgingBucket GrandTotal { get; set; } = new();
}
