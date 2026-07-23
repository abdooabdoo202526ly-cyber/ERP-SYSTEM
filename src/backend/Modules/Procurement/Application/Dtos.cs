using System;
using System.Collections.Generic;
using ERPSystem.Modules.Procurement.Entities;

namespace ERPSystem.Modules.Procurement.Application;

// ============== Vendor DTOs ==============

public sealed class CreateVendorRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Website { get; set; }  // DEC-081b
    public string Currency { get; set; } = "LYD";
    public string PaymentTerms { get; set; } = "Net30";
}

public sealed class UpdateVendorRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Website { get; set; }  // DEC-081c
    public string Currency { get; set; } = "LYD";
    public string PaymentTerms { get; set; } = "Net30";
    public bool IsActive { get; set; } = true;
}

public sealed class VendorResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? TaxNumber { get; set; }
    public string? Website { get; set; }  // DEC-081b
    public string Currency { get; set; } = "LYD";
    public string PaymentTerms { get; set; } = "Net30";
    public bool IsActive { get; set; }
}

// ============== PurchaseOrder DTOs ==============

public sealed class CreatePurchaseOrderLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class CreatePurchaseOrderRequest
{
    public Guid VendorId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? ExpectedDate { get; set; }
    public string Currency { get; set; } = "LYD";
    public string? Notes { get; set; }
    /// <summary>v1.0.34: اختياري. عند الإضافة، يتم اشتقاق مركز التكلفة منه لو لم يحدد المستخدم.</summary>
    public Guid? ProjectId { get; set; }
    /// <summary>v1.0.34: مركز التكلفة. إن لم يحدد، يُشتق من ProjectId أو يأخذ PROJ-DEFAULT.</summary>
    public Guid? CostCenterId { get; set; }
    public List<CreatePurchaseOrderLineRequest> Lines { get; set; } = new();
}

public sealed class PurchaseOrderLineResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal SubTotal { get; set; }
    public int LineOrder { get; set; }
}

public sealed class PurchaseOrderResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string PoNumber { get; set; } = string.Empty;
    public Guid VendorId { get; set; }
    public string? VendorName { get; set; }  // v1.0.33: resolved server-side
    public Guid? ProjectId { get; set; }          // v1.0.34
    public string? ProjectName { get; set; }      // v1.0.34
    public Guid? CostCenterId { get; set; }       // v1.0.34
    public string? CostCenterName { get; set; }   // v1.0.34
    public PurchaseOrderStatus Status { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ExpectedDate { get; set; }
    public string Currency { get; set; } = "LYD";
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedBy { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PurchaseOrderLineResponse> Lines { get; set; } = new();
}

// ============== GoodsReceipt DTOs ==============

public sealed class CreateGoodsReceiptLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateGoodsReceiptRequest
{
    public Guid PurchaseOrderId { get; set; }
    public DateTime ReceivedDate { get; set; } = DateTime.UtcNow;
    public Guid WarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<CreateGoodsReceiptLineRequest> Lines { get; set; } = new();
}

public sealed class GoodsReceiptLineResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
    public int LineOrder { get; set; }
}

public sealed class GoodsReceiptResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string GrNumber { get; set; } = string.Empty;
    public Guid PurchaseOrderId { get; set; }
    public string? PoNumber { get; set; }             // DEC-031: enriched
    public string? PoStatus { get; set; }             // DEC-031: enriched
    public GoodsReceiptStatus Status { get; set; }
    public DateTime ReceivedDate { get; set; }
    public Guid WarehouseId { get; set; }
    public string? WarehouseName { get; set; }        // DEC-031: enriched
    public string? WarehouseCode { get; set; }        // DEC-031: enriched
    public Guid? VendorId { get; set; }               // DEC-031: via PO
    public string? VendorName { get; set; }           // DEC-031: via PO
    public string? VendorCode { get; set; }           // DEC-031: via PO
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GoodsReceiptLineResponse> Lines { get; set; } = new();
}

// ============== VendorBill DTOs ==============

public sealed class CreateVendorBillLineRequest
{
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
}

public sealed class CreateVendorBillRequest
{
    public Guid GoodsReceiptId { get; set; }
    public DateTime BillDate { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; } = "LYD";
    public string? Notes { get; set; }
    public List<CreateVendorBillLineRequest> Lines { get; set; } = new();
}

public sealed class VendorBillLineResponse
{
    public Guid Id { get; set; }
    public Guid ItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TaxRate { get; set; }
    public decimal SubTotal { get; set; }
    public int LineOrder { get; set; }
}

public sealed class VendorBillResponse
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string BillNumber { get; set; } = string.Empty;
    public Guid GoodsReceiptId { get; set; }
    public Guid VendorId { get; set; }
    public VendorBillStatus Status { get; set; }
    public DateTime BillDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string Currency { get; set; } = "LYD";
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public Guid? JournalEntryId { get; set; }
    public DateTime? PostedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<VendorBillLineResponse> Lines { get; set; } = new();
}
