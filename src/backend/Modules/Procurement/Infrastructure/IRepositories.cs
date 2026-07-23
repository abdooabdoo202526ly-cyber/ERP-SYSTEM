using System;
using System.Collections.Generic;
using ERPSystem.Modules.Procurement.Entities;

namespace ERPSystem.Modules.Procurement.Infrastructure;

/// <summary>Repository contracts للمورّدين.</summary>
public interface IVendorRepository
{
    Task<Vendor?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<Vendor?> GetByCodeAsync(Guid tenantId, string code, CancellationToken ct);
    Task<IReadOnlyList<Vendor>> ListAsync(Guid tenantId, bool includeInactive, int skip, int take, CancellationToken ct);
    Task<IReadOnlyList<Vendor>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct); // DEC-031
    Task InsertAsync(Vendor vendor, CancellationToken ct);
    Task UpdateAsync(Vendor vendor, CancellationToken ct);
}

/// <summary>Repository contracts لأوامر الشراء.</summary>
public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<PurchaseOrder?> GetByPoNumberAsync(Guid tenantId, string poNumber, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(Guid tenantId, Guid? vendorId, PurchaseOrderStatus? status, int skip, int take, CancellationToken ct);
    Task InsertAsync(PurchaseOrder po, CancellationToken ct);
    Task UpdateAsync(PurchaseOrder po, CancellationToken ct);
    Task InsertLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct);
    Task UpdateLinesAsync(Guid tenantId, Guid poId, IEnumerable<PurchaseOrderLine> lines, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrderLine>> GetLinesAsync(Guid poId, CancellationToken ct);
    Task<IReadOnlyList<PurchaseOrder>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct); // DEC-031
}

/// <summary>Repository contracts لسندات الاستلام.</summary>
public interface IGoodsReceiptRepository
{
    Task<GoodsReceipt?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<GoodsReceipt?> GetByGrNumberAsync(Guid tenantId, string grNumber, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceipt>> ListAsync(Guid tenantId, Guid? poId, GoodsReceiptStatus? status, int skip, int take, CancellationToken ct);
    Task InsertAsync(GoodsReceipt gr, CancellationToken ct);
    Task UpdateAsync(GoodsReceipt gr, CancellationToken ct);
    Task InsertLinesAsync(Guid tenantId, Guid grId, IEnumerable<GoodsReceiptLine> lines, CancellationToken ct);
    Task<IReadOnlyList<GoodsReceiptLine>> GetLinesAsync(Guid grId, CancellationToken ct);
}

/// <summary>Repository contracts لفواتير المورّدين.</summary>
public interface IVendorBillRepository
{
    Task<VendorBill?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<VendorBill?> GetByBillNumberAsync(Guid tenantId, string billNumber, CancellationToken ct);
    Task<IReadOnlyList<VendorBill>> ListAsync(Guid tenantId, Guid? vendorId, Guid? grId, VendorBillStatus? status, int skip, int take, CancellationToken ct);
    Task InsertAsync(VendorBill bill, CancellationToken ct);
    Task UpdateAsync(VendorBill bill, CancellationToken ct);
    Task InsertLinesAsync(Guid tenantId, Guid billId, IEnumerable<VendorBillLine> lines, CancellationToken ct);
    Task<IReadOnlyList<VendorBillLine>> GetLinesAsync(Guid billId, CancellationToken ct);
}

/// <summary>عداد أرقام المستندات — يُستخدم لتوليد PO/GR/Bill numbers تلقائياً.</summary>
public interface IDocumentSequenceRepository
{
    /// <summary>يُرجع الرقم التسلسلي التالي لمستند معين (PO/GR/BILL) داخل الـ tenant.</summary>
    Task<string> GetNextNumberAsync(Guid tenantId, string prefix, CancellationToken ct);
}
