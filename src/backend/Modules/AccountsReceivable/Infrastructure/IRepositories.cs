using System;
using System.Collections.Generic;
using ERPSystem.Modules.AccountsReceivable.Application;
using ERPSystem.Modules.AccountsReceivable.Entities;

namespace ERPSystem.Modules.AccountsReceivable.Infrastructure;

// ============== Customer ==============
public interface ICustomerRepository
{
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Customer?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Customer>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task InsertAsync(Customer c, CancellationToken ct);
    Task UpdateAsync(Customer c, CancellationToken ct);
}

// ============== SalesInvoice ==============
public interface ISalesInvoiceRepository
{
    Task<SalesInvoice?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<SalesInvoice?> GetByInvoiceNumberAsync(Guid tenantId, string invoiceNumber, CancellationToken ct);
    Task<IReadOnlyList<SalesInvoice>> ListAsync(Guid tenantId, Guid? customerId, SalesInvoiceStatus? status, int skip, int take, CancellationToken ct);
    Task InsertAsync(SalesInvoice inv, CancellationToken ct);
    Task UpdateAsync(SalesInvoice inv, CancellationToken ct);
    Task InsertLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct);
    Task UpdateLinesAsync(Guid tenantId, Guid salesInvoiceId, IEnumerable<SalesInvoiceLine> lines, CancellationToken ct);
    Task<IReadOnlyList<SalesInvoiceLine>> GetLinesAsync(Guid salesInvoiceId, CancellationToken ct);
    /// <summary>يجمع مجموع المدفوع لفاتورة معينة (يُخصم من التخصيصات).</summary>
    Task<decimal> GetTotalAllocatedAsync(Guid tenantId, Guid salesInvoiceId, CancellationToken ct);
    /// <summary>يفوتر Open invoices (المتبقي > 0) لعميل — لتقرير Aging + نموذج تخصيص القبض.</summary>
    Task<IReadOnlyList<SalesInvoice>> ListOpenByCustomerAsync(Guid tenantId, Guid customerId, CancellationToken ct);
    Task<IReadOnlyList<SalesInvoice>> ListAllOpenAsync(Guid tenantId, CancellationToken ct);
}

// ============== Receipt ==============
public interface IReceiptRepository
{
    Task<Receipt?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Receipt?> GetByReceiptNumberAsync(Guid tenantId, string receiptNumber, CancellationToken ct);
    Task<IReadOnlyList<Receipt>> ListAsync(Guid tenantId, Guid? customerId, int skip, int take, CancellationToken ct);
    Task InsertAsync(Receipt r, CancellationToken ct);
    Task UpdateAsync(Receipt r, CancellationToken ct);
    Task InsertAllocationsAsync(Guid tenantId, Guid receiptId, IEnumerable<ReceiptAllocation> allocations, CancellationToken ct);
    Task<IReadOnlyList<ReceiptAllocation>> GetAllocationsAsync(Guid receiptId, CancellationToken ct);
}

// ============== Document Sequence ==============
/// <summary>عداد أرقام المستندات — يُستخدم لتوليد SI/RC numbers تلقائياً.</summary>
public interface IArDocumentSequenceRepository
{
    Task<string> GetNextNumberAsync(Guid tenantId, string prefix, CancellationToken ct);
}
