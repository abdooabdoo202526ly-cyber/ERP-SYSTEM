using ERPSystem.Modules.Procurement.Application;
using ERPSystem.Modules.Procurement.Entities;
using FluentValidation;

namespace ERPSystem.Modules.Procurement.Application;

/// <summary>
/// FluentValidation rules لكل Request DTOs في Procurement.
/// ملاحظة: اسم كل class ينتهي بـ RequestValidator لتوافقه مع
/// AddValidatorsFromAssemblyContaining في Program.cs.
/// </summary>
public sealed class CreateVendorRequestValidator : AbstractValidator<CreateVendorRequest>
{
    public CreateVendorRequestValidator()
    {
        RuleFor(x => x.Code).NotEmpty().MaximumLength(50)
            .Matches("^[A-Za-z0-9\\-_]+$").WithMessage("Code: حروف/أرقام/-/_ فقط.");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PaymentTerms).NotEmpty().Must(t => PaymentTerms.All.Contains(t))
            .WithMessage($"PaymentTerms يجب أن يكون واحداً من: {string.Join(", ", PaymentTerms.All)}");
    }
}

public sealed class UpdateVendorRequestValidator : AbstractValidator<UpdateVendorRequest>
{
    public UpdateVendorRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Email).MaximumLength(200).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.TaxNumber).MaximumLength(50);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.PaymentTerms).NotEmpty().Must(t => PaymentTerms.All.Contains(t))
            .WithMessage($"PaymentTerms يجب أن يكون واحداً من: {string.Join(", ", PaymentTerms.All)}");
    }
}

public sealed class CreatePurchaseOrderLineRequestValidator : AbstractValidator<CreatePurchaseOrderLineRequest>
{
    public CreatePurchaseOrderLineRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxRate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1);
    }
}

public sealed class CreatePurchaseOrderRequestValidator : AbstractValidator<CreatePurchaseOrderRequest>
{
    public CreatePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.VendorId).NotEqual(Guid.Empty);
        RuleFor(x => x.OrderDate).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("يجب أن يحتوي PO على بند واحد على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new CreatePurchaseOrderLineRequestValidator());
        RuleFor(x => x.ExpectedDate).GreaterThanOrEqualTo(x => x.OrderDate.Date)
            .When(x => x.ExpectedDate.HasValue)
            .WithMessage("تاريخ التوقع المتوقع يجب أن يكون >= تاريخ الطلب.");
    }
}

public sealed class CreateGoodsReceiptLineRequestValidator : AbstractValidator<CreateGoodsReceiptLineRequest>
{
    public CreateGoodsReceiptLineRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public sealed class CreateGoodsReceiptRequestValidator : AbstractValidator<CreateGoodsReceiptRequest>
{
    public CreateGoodsReceiptRequestValidator()
    {
        RuleFor(x => x.PurchaseOrderId).NotEqual(Guid.Empty);
        RuleFor(x => x.WarehouseId).NotEqual(Guid.Empty);
        RuleFor(x => x.ReceivedDate).NotEmpty();
        RuleFor(x => x.Lines).NotEmpty().WithMessage("يجب أن يحتوي GR على بند واحد على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new CreateGoodsReceiptLineRequestValidator());
    }
}

public sealed class CreateVendorBillLineRequestValidator : AbstractValidator<CreateVendorBillLineRequest>
{
    public CreateVendorBillLineRequestValidator()
    {
        RuleFor(x => x.ItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxRate).GreaterThanOrEqualTo(0).LessThanOrEqualTo(1);
    }
}

public sealed class CreateVendorBillRequestValidator : AbstractValidator<CreateVendorBillRequest>
{
    public CreateVendorBillRequestValidator()
    {
        RuleFor(x => x.GoodsReceiptId).NotEqual(Guid.Empty);
        RuleFor(x => x.BillDate).NotEmpty();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("يجب أن تحتوي الفاتورة على بند واحد على الأقل.");
        RuleForEach(x => x.Lines).SetValidator(new CreateVendorBillLineRequestValidator());
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.BillDate.Date)
            .When(x => x.DueDate.HasValue)
            .WithMessage("تاريخ الاستحقاق يجب أن يكون >= تاريخ الفاتورة.");
    }
}
